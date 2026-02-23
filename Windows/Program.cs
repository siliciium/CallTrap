/*
Copyright 2026 Silicium

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

    http://www.apache.org/licenses/LICENSE-2.0
*/


using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

using SIPSorcery.Net;
using SIPSorcery.SIP;
using SIPSorcery.SIP.App;
using SIPSorcery.Sys;

using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Softphone
{

    internal class Program
    {

        private static string sip_user = null;
        private static string sip_pwd = null;
        private static string sip_server = null;
        private static string sip_domain = null;
        private static bool byeHandled = false;
        private static string localIP = null;

        private static string dstNumber = null;
        private static int sip_port = 5060;

        private static string sipUri = null;

        private static BufferedWaveProvider waveProvider;
        private static WaveOutEvent waveOut;

        private static WasapiCapture micCapture;
        private static BufferedWaveProvider micBuffer;
        private static bool UseMic = false;
        private static ISampleProvider resampler;

        private static WaveFileReader wavReader;
        private static bool rtpReady = false;
        private static string audioFile = null;
        private static bool AudioFileSend = false;

        private static WaveFileWriter wavRemote;
        private static WaveFileWriter wavLocal;
        private static bool Record = false;
        private static WaveFileWriter wavStereo;
        private static short[] lastLocalFrame;

        private static SIPClientUserAgent uac;
        private static SIPTransport sipTransport;
        private static SIPRegistrationUserAgent regUserAgent;
        private static SIPCallDescriptor callDescriptor;
        private static RTPChannel rtpChannel;

        private static bool isInCall = false;
        private static bool isHelp = false;
        private static bool Verbose = false;
        private static readonly object consoleLock = new object();
        private static bool footerEnabled = true;
        static int logStartLine = 2;
        private static string logicon = "\u001b[93m◉\u001b[0m";

        [STAThread]
        static async Task Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            try
            {

                for (int i = 0; i < args.Length; i++)
                {
                    switch (args[i])
                    {
                        case "--help":
                            isHelp = true;
                            break;

                        case "-h":
                            isHelp = true;
                            break;

                        case "--sip-server":
                            sip_server = args[++i];
                            break;

                        case "--sip-user":
                            sip_user = args[++i];
                            break;

                        case "--sip-pwd":
                            sip_pwd = args[++i];
                            break;

                        case "--callnum":
                            dstNumber = args[++i];
                            break;

                        case "--audio-file":
                            audioFile = args[++i];
                            AudioFileSend = true;
                            break;

                        case "--mic":
                            UseMic = true;
                            break;

                        case "--rec":
                            Record = true;
                            break;

                        case "--verbose":
                            Verbose = true;
                            break;
                    }
                }

                if (isHelp)
                {
                    Help(String.Empty);
                }

                if (null == sip_server)
                {
                    Help("? Missing argument : --sip-server <addr|name>");
                }
                else if (null == sip_user)
                {
                    Help("? Missing argument : --sip-user <usr>");
                }
                else if (null == sip_pwd)
                {
                    Help("? Missing argument : --sip-pwd <pwd>");
                }
                else if (null == dstNumber)
                {
                    Help("? Missing argument : --callnum <num>");
                }
                else if (UseMic && null != audioFile)
                {
                    Help("? Incompatibles arguments : --mic and --audio-file <file>");
                }
                else if (null != audioFile && !System.IO.File.Exists(audioFile))
                {
                    Help($"? File not found : {audioFile}");
                }
                else
                {
                    Console.Clear();
                    DrawHeader();

                    Log("    |\\__/|", true);
                    Log("    ( - -)", true);
                    Log("   / >[+XX] SOFTPHONE v0.1", true);
                    Log("   ", true);

                    if (null != audioFile && System.IO.File.Exists(audioFile))
                    {

                        FileInfo f = new FileInfo(audioFile);

                        if (IsWavExt(audioFile))
                        {
                            if (!IsWav(audioFile))
                            {
                                Log($"? File {f.Name} have .wav extension but is not real WAV.");
                                Environment.Exit(1);
                            }
                        }
                        else if (IsMp3Ext(audioFile))
                        {
                            if (IsMp3(audioFile))
                            {
                                string out_path = $"{f.DirectoryName}\\{f.Name.Replace(f.Extension, ".wav")}";
                                if (!System.IO.File.Exists(out_path))
                                {
                                    Information($"[*] {f.Name} is MP3, converting to WAV...");

                                    Mp3ToWav(audioFile, out_path);

                                    audioFile = out_path;
                                }
                                else
                                {
                                    Log($"? Unable to convert MP3 to WAV : {f.Name.Replace(f.Extension, ".wav")} already exists.");
                                    Environment.Exit(1);
                                }

                            }
                        }
                        else
                        {
                            Log($"? File {f.Name} is not supported (only WAV or MP3).");
                            Environment.Exit(1);
                        }

                    }

                    sip_domain = ResolveToIPAddress(sip_server);

                    if (sip_domain == null)
                    {
                        Log("? Unable to resolve SIP domain to an IP.");
                        Environment.Exit(1);
                    }
                    else
                    {
                        IPAddress _localIP = GetLocalAddressForRemote(sip_domain);

                        if (_localIP == null)
                        {
                            Log("? No network interface matching the SIP server subnet could be found.");
                            Environment.Exit(1);
                        }
                        else
                        {
                            sipUri = $"sip:{dstNumber}@{sip_domain}";
                            localIP = _localIP.ToString();
                            Log($"{logicon} Local interface used : {localIP}");

                        }
                    }

                }
            }
            catch (Exception ex)
            {
                Help($"? Error ");
                Environment.Exit(1);
            }

            if (UseMic)
            {
                InitMic();
            }

            if (Record)
            {
                InitRecord();
            }

            if (AudioFileSend)
            {
                InitAudioFileSend();
            }

            Information("=== SIP Client Start ===");

            sipTransport = new SIPTransport();
            sipTransport.AddSIPChannel(new SIPUDPChannel(new IPEndPoint(IPAddress.Any, sip_port)));

            sipTransport.SIPTransportRequestReceived += async (local, remote, req) =>
            {

                if (req.Method == SIPMethodsEnum.BYE)
                {
                    if (!byeHandled && req.Method == SIPMethodsEnum.BYE)
                    {
                        byeHandled = true;
                        isInCall = false;
                        Information($"{Environment.NewLine}{req.ToString().TrimEnd(Environment.NewLine.ToCharArray())}{Environment.NewLine}");
                        Log($"{logicon} Call hung up (BYE received).");

                        CleanAfterCall();

                        Environment.Exit(0);

                    }
                }

                await Task.CompletedTask;
            };

            uac = new SIPClientUserAgent(sipTransport, null);

            regUserAgent = new SIPRegistrationUserAgent(
                sipTransport,
                sip_user,
                sip_pwd,
                sip_domain,
                expiry: 300);

            regUserAgent.RegistrationSuccessful += (uri, resp) =>
            {
                Log($"{logicon} REGISTER OK: {uri}");
                if (resp != null)
                {
                    Information($"[*] Response: {resp.StatusCode} {resp.ReasonPhrase.TrimEnd(Environment.NewLine.ToCharArray())}");

                    rtpChannel = new RTPChannel(
                        false,
                        IPAddress.Parse(localIP),
                        0,
                        new PortRange(10000, 20000)
                    );

                    rtpChannel.OnRTPDataReceived += (localPort, remoteEP, packet) =>
                    {
                        rtpReady = true;

                        if (packet.Length < 12)
                            return;

                        int headerSize = 12;
                        int payloadSize = packet.Length - headerSize;

                        byte[] payload = new byte[payloadSize];
                        Buffer.BlockCopy(packet, headerSize, payload, 0, payloadSize);

                        byte[] pcm = new byte[payload.Length * 2];
                        int offset = 0;

                        foreach (byte alaw in payload)
                        {
                            short sample = NAudio.Codecs.ALawDecoder.ALawToLinearSample(alaw);
                            pcm[offset++] = (byte)(sample & 0xFF);
                            pcm[offset++] = (byte)((sample >> 8) & 0xFF);
                        }

                        waveProvider.AddSamples(pcm, 0, pcm.Length);

                        if (Record)
                        {

                            if (null != wavStereo)
                            {

                                byte[] stereoBytes = new byte[160 * 4];
                                int _offset = 0;

                                if (lastLocalFrame == null || lastLocalFrame.Length < 160)
                                {

                                    lastLocalFrame = new short[160];
                                }

                                for (int i = 0; i < 160; i++)
                                {
                                    short left = lastLocalFrame[i];
                                    short right = BitConverter.ToInt16(pcm, i * 2);

                                    stereoBytes[_offset++] = (byte)(left & 0xFF);
                                    stereoBytes[_offset++] = (byte)((left >> 8) & 0xFF);

                                    stereoBytes[_offset++] = (byte)(right & 0xFF);
                                    stereoBytes[_offset++] = (byte)((right >> 8) & 0xFF);
                                }

                                lock (wavStereo)
                                {
                                    wavStereo.Write(stereoBytes, 0, stereoBytes.Length);
                                }
                            }

                        }

                    };

                    rtpChannel.StartRtpReceiver();

                    int localRtpPort = rtpChannel.RTPPort;

                    string localSdp =
                        "v=0\r\n" +
                        "o=- 12345 2 IN IP4 " + localIP + "\r\n" +
                        "s=SIP Call\r\n" +
                        "c=IN IP4 " + localIP + "\r\n" +
                        "t=0 0\r\n" +
                        "m=audio " + localRtpPort + " RTP/AVP 8 101\r\n" +
                        "a=rtpmap:8 PCMA/8000\r\n" +
                        "a=rtpmap:101 telephone-event/8000\r\n" +
                        "a=fmtp:101 0-16\r\n";

                    callDescriptor = new SIPCallDescriptor(
                        sip_user,
                        sip_pwd,
                        $"sip:{dstNumber}@{sip_domain}",
                        $"sip:{sip_user}@{sip_domain}",
                        null,
                        $"sip:{sip_user}@{localIP}",
                        null,
                        null,
                        SIPCallDirection.Out,
                        SDP.SDP_MIME_CONTENTTYPE,
                        localSdp,
                        null
                    );

                    uac.CallTrying += (ua, _resp) =>
                    {
                        Log($"{logicon} Call Trying {dstNumber}...");
                    };

                    uac.CallRinging += (ua, _resp) =>
                    {
                        Log($"{logicon} Call Ringing...");
                    };

                    uac.CallAnswered += (ua, _resp) =>
                    {
                        isInCall = true;

                        SoftPhone.RingTone.StopRingTone();

                        Information($"{Environment.NewLine}{_resp.ToString().TrimEnd(Environment.NewLine.ToCharArray())}{Environment.NewLine}");

                        Log($"{logicon} Call Answered!");

                        if (_resp.Status == SIPResponseStatusCodesEnum.NotFound)
                        {
                            Log($"? The number {dstNumber} is invalid.");
                            return;
                        }
                        else if (_resp.Status != SIPResponseStatusCodesEnum.Ok)
                        {
                            Log($"? Call failed ({_resp.Status}).");
                            return;
                        }

                        SDP remoteSdp = SDP.ParseSDPDescription(_resp.Body);
                        if (remoteSdp.Media.Count > 0)
                        {
                            int remoteRtpPort = remoteSdp.Media[0].Port;
                            string remoteRtpIP = remoteSdp.Connection.ConnectionAddress;
                            Information($"[*] RTP remote : {remoteRtpIP}:{remoteRtpPort}");

                            var remoteEP = new IPEndPoint(IPAddress.Parse(remoteRtpIP), remoteRtpPort);
                            Information($"[*] rtpChannel hash = {rtpChannel.GetHashCode()}");

                            if (AudioFileSend && (null != waveOut))
                            {
                                Task.Run(async () =>
                                {

                                    await Task.Delay(200);

                                    Information($"[*] SendFromFile {audioFile}");
                                    await SendFromFile(rtpChannel, remoteEP, audioFile);
                                });
                            }

                            if (UseMic && (null != micCapture))
                            {
                                Task.Run(async () =>
                                {

                                    await Task.Delay(200);

                                    while (!rtpReady)
                                        await Task.Delay(10);

                                    await StreamMic(resampler, rtpChannel, remoteEP);
                                });
                            }

                        }

                    };

                    uac.CallFailed += (ua, err, sipResponse) =>
                    {
                        Log($"? Call failed : {err}");

                        if (err.Contains("404"))
                        {
                            Log($"? Number {dstNumber} does not exist !");
                        }

                        if (sipResponse != null)
                        {
                            Information($"[*] SIP Response: {sipResponse.StatusCode} {sipResponse.ReasonPhrase}");
                        }

                    };

                    uac.Call(callDescriptor);

                }

            };

            regUserAgent.RegistrationFailed += (uri, resp, msg) =>
            {
                Log($"? REGISTER FAILED: {msg}");
                if (resp != null)
                {
                    if (Verbose)
                    {
                        Information($"[*] Response: {resp.StatusCode} {resp.ReasonPhrase.TrimEnd(Environment.NewLine.ToCharArray())}");
                    }
                }
            };

            SoftPhone.RingTone.StartRingTone(SoftPhone.RingTone.CountryTone.Perso);

            Log($"{logicon} Send REGISTER ...");
            regUserAgent.Start();

            var key = Console.ReadKey(true);
            if (key.Key == ConsoleKey.Enter)
            {

                Log($"{logicon} HangHup !");

                SoftPhone.RingTone.StopRingTone();
                uac.Hangup();

            }

            footerEnabled = false;

            Console.SetCursorPosition(0, Console.WindowHeight - 1);
            Console.Write(new string(' ', Console.WindowWidth));
            Console.SetCursorPosition(0, Console.WindowHeight - 1);

        }

        private static void UserAgent_ClientCallFailed(ISIPClientUserAgent uac, string errorMessage, SIPResponse sipResponse)
        {
            throw new NotImplementedException();
        }

        private static void Mp3ToWav(string mp3Path, string wavPath)
        {
            using (var reader = new Mp3FileReader(mp3Path))
            {
                var newFormat = new WaveFormat(44100, 16, 2);
                using (var conversionStream = new WaveFormatConversionStream(newFormat, reader))
                using (var writer = new WaveFileWriter(wavPath, conversionStream.WaveFormat))
                {
                    conversionStream.CopyTo(writer);
                }
            }
        }

        private static bool IsMp3(string path)
        {
            try
            {
                var reader = new Mp3FileReader(path);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsMp3Ext(string path) => Path.GetExtension(path).Equals(".mp3", StringComparison.OrdinalIgnoreCase);

        private static bool IsWav(string path)
        {
            try
            {
                var reader = new WaveFileReader(path);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsWavExt(string path) => Path.GetExtension(path).Equals(".wav", StringComparison.OrdinalIgnoreCase);

        private static async Task SendFromFile(RTPChannel rtpChannel, EndPoint remoteEP, string audio_file)
        {
            Information($"[*][SendFromFile] Task Started");
            Information($"[*][SendFromFile] rtpChannel hash = {rtpChannel.GetHashCode()}");

            wavReader = new WaveFileReader(audio_file);
            Information($"[*][SendFromFile] {wavReader.WaveFormat.ToString()}");
            Information($"[*][SendFromFile] WAV length = {wavReader.Length} bytes");
            Information($"[*][SendFromFile] WAV format = {wavReader.WaveFormat}");

            ISampleProvider sampleProvider = wavReader.ToSampleProvider();

            if (wavReader.WaveFormat.Channels == 2)
            {
                var stereo = sampleProvider;
                var mono = new StereoToMonoSampleProvider(stereo);
                mono.LeftVolume = 0.5f;
                mono.RightVolume = 0.5f;
                sampleProvider = mono;

                Information($"[*][SendFromFile] {audio_file} stéréo ? mono");
            }
            else
            {
                Information($"[*][SendFromFile] {audio_file} is mono");
            }

            if (wavReader.WaveFormat.SampleRate != 8000)
            {
                sampleProvider = new WdlResamplingSampleProvider(sampleProvider, 8000);
                Information($"[*][SendFromFile] {audio_file} {wavReader.WaveFormat.SampleRate} Khz ? 8000 Khz");
            }
            else
            {
                Information($"[*][SendFromFile] {audio_file} is {wavReader.WaveFormat.SampleRate} Khz");
            }

            var sw = new Stopwatch();
            sw.Start();
            long nextTick = 0;

            ushort seq = 1;
            uint timestamp = 0;

            float[] floatBuffer = new float[160];
            byte[] pcm = new byte[160 * 2];
            byte[] alaw = new byte[160];

            bool eof = false;
            try
            {
                while (!eof)
                {
                    int read = sampleProvider.Read(floatBuffer, 0, 160);

                    if (read == 0)
                    {
                        eof = true;
                        break;
                    }

                    if (read < floatBuffer.Length)
                    {

                        for (int i = read; i < floatBuffer.Length; i++)
                            floatBuffer[i] = 0f;

                        eof = true;
                    }

                    for (int i = 0; i < read; i++)
                    {
                        short sample = (short)(floatBuffer[i] * short.MaxValue);
                        pcm[i * 2] = (byte)(sample & 0xFF);
                        pcm[i * 2 + 1] = (byte)((sample >> 8) & 0xFF);
                    }

                    if (Record)
                    {

                        if (wavLocal != null)
                        {
                            lock (wavLocal)
                            {
                                wavLocal.Write(pcm, 0, read * 2);
                            }
                        }
                    }

                    for (int i = 0; i < read; i++)
                    {
                        short sample = (short)(pcm[i * 2] | (pcm[i * 2 + 1] << 8));
                        alaw[i] = NAudio.Codecs.ALawEncoder.LinearToALawSample(sample);
                    }

                    byte[] payload = new byte[read];
                    Array.Copy(alaw, payload, read);

                    var segment = new ArraySegment<byte>(payload, 0, read);

                    var rtpPacket = new RTPPacket(segment, 0);

                    rtpPacket.Header.PayloadType = 8;
                    rtpPacket.Header.SequenceNumber = seq++;
                    rtpPacket.Header.Timestamp = timestamp;
                    rtpPacket.Header.SyncSource = 1234;

                    byte[] buffer = rtpPacket.GetBytes();

                    long now = sw.ElapsedMilliseconds;
                    if (now < nextTick)
                        await Task.Delay((int)(nextTick - now));

                    rtpChannel.RtpSocket.SendTo(buffer, remoteEP);

                    nextTick += 20;
                    timestamp += 160;

                }
            }
            catch (Exception ex)
            {

            }

            Information($"[*][SendFromFile] Task Finished");
        }

        private static bool InitAudioFileSend()
        {
            waveProvider = new BufferedWaveProvider(new WaveFormat(8000, 16, 1));
            waveProvider.BufferDuration = TimeSpan.FromMilliseconds(200);
            waveProvider.DiscardOnBufferOverflow = true;

            waveOut = new WaveOutEvent();
            waveOut.DesiredLatency = 150;
            waveOut.NumberOfBuffers = 3;
            waveOut.Init(waveProvider);
            waveOut.Play();

            return true;
        }

        private static bool InitRecord()
        {
            string basePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            WaveFormat stereoFormat = new WaveFormat(8000, 16, 2);
            wavStereo = new WaveFileWriter(Path.Combine(basePath, "Downloads", $"local_{dstNumber}_{DateTime.Now:yyyyMMdd_HHmmss}.wav"),
                new WaveFormat(8000, 16, 2));

            Information("[REC] Record : stereo, 1 canal local, 1 canal remote");

            return true;
        }

        private static bool IsValidMicrophone(MMDevice device)
        {
            if (device == null)
                return false;

            string name = device.FriendlyName.ToLower();

            if (name.Contains("mixage") || name.Contains("stereo mix") || name.Contains("loopback"))
                return false;

            return true;
        }

        private static void InitMic()
        {

            var enumerator = new MMDeviceEnumerator();

            MMDevice mic = null;

            try
            {
                mic = enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Multimedia);
            }
            catch
            {
                mic = null;
            }

            if (!IsValidMicrophone(mic))
            {
                Log("? No valid microphone was found.");
                Environment.Exit(1);
            }

            Log($"{logicon} Using Microphone : {mic.FriendlyName}");
            Information($"\t\tSampleRate    : {mic.AudioClient.MixFormat.SampleRate} Hz");
            Information($"\t\tBitsPerSample : {mic.AudioClient.MixFormat.BitsPerSample} bits");
            Information($"\t\tChannels      : {mic.AudioClient.MixFormat.Channels} canaux");

            int rate = mic.AudioClient.MixFormat.SampleRate;
            int bits = mic.AudioClient.MixFormat.BitsPerSample;
            int channels = mic.AudioClient.MixFormat.Channels;

            micCapture = new WasapiCapture(mic);

            micBuffer = new BufferedWaveProvider(micCapture.WaveFormat)
            {
                DiscardOnBufferOverflow = true
            };

            if (channels == 2)
            {
                Information($"[MIC] {mic.FriendlyName} Stéréo ? Mono");

                var inputProvider = micBuffer.ToSampleProvider();

                var mono = new StereoToMonoSampleProvider(inputProvider)
                {
                    LeftVolume = 1.0f,
                    RightVolume = 1.0f
                };

                if (rate != 8000)
                {
                    Information($"[MIC] {mic.FriendlyName} {rate} hz ? 8000 Hz");

                    resampler = new WdlResamplingSampleProvider(
                        mono,
                        8000
                    );
                }

            }
            else
            {
                if (rate != 8000)
                {
                    Information($"[MIC] {mic.FriendlyName} {rate} Khz ? 8000 Hz");

                    resampler = new WdlResamplingSampleProvider(
                        micBuffer.ToSampleProvider(),
                        8000
                    );
                }
            }

            micCapture.DataAvailable += (s, a) =>
            {
                micBuffer.AddSamples(a.Buffer, 0, a.BytesRecorded);
            };

            micCapture.StartRecording();

        }

        private static async Task StreamMic(ISampleProvider resampler, RTPChannel rtpChannel, EndPoint remoteEP)
        {
            Information($"[*][StreamMic] Task Started");

            ushort seq = 1;
            uint timestamp = 0;

            float[] floatFrame = new float[160];
            byte[] alawFrame = new byte[160];

            var sw = new Stopwatch();
            sw.Start();
            long nextTick = 0;

            while (true)
            {

                int read = resampler.Read(floatFrame, 0, 160);
                if (read < 160)
                {

                    for (int i = read; i < 160; i++)
                        floatFrame[i] = 0;
                }

                float targetLevel = 0.25f;
                float maxGain = 10.0f;
                float attack = 0.01f;
                float release = 0.0005f;

                float sum = 0;
                for (int i = 0; i < 160; i++)
                    sum += floatFrame[i] * floatFrame[i];

                float rms = (float)Math.Sqrt(sum / 160f);
                float gain = 1.0f;

                if (rms > 0.0001f)
                {
                    float desiredGain = targetLevel / rms;

                    if (desiredGain > gain)
                        gain += attack * (desiredGain - gain);
                    else
                        gain += release * (desiredGain - gain);

                    if (gain > maxGain)
                        gain = maxGain;
                }

                for (int i = 0; i < 160; i++)
                    floatFrame[i] *= gain;

                short[] pcm = new short[160];
                for (int i = 0; i < 160; i++)
                    pcm[i] = (short)(floatFrame[i] * short.MaxValue);

                if (Record && wavStereo != null)
                {

                    lastLocalFrame = pcm;
                }

                for (int i = 0; i < 160; i++)
                    alawFrame[i] = NAudio.Codecs.ALawEncoder.LinearToALawSample(pcm[i]);

                var segment = new ArraySegment<byte>(alawFrame, 0, 160);
                var rtpPacket = new RTPPacket(segment, 0);

                rtpPacket.Header.PayloadType = 8;
                rtpPacket.Header.SequenceNumber = seq++;
                rtpPacket.Header.Timestamp = timestamp;
                rtpPacket.Header.SyncSource = 1234;

                byte[] buffer = rtpPacket.GetBytes();

                long now = sw.ElapsedMilliseconds;
                if (now < nextTick)
                    await Task.Delay((int)(nextTick - now));

                rtpChannel.RtpSocket.SendTo(buffer, remoteEP);

                nextTick += 20;
                timestamp += 160;
            }

            Information($"[*][StreamMic] Task Finished");
        }

        private static string ResolveToIPAddress(string sip_server)
        {

            if (IPAddress.TryParse(sip_server, out IPAddress ip))
                return ip.ToString();

            try
            {
                var addresses = Dns.GetHostAddresses(sip_server);

                foreach (var addr in addresses)
                {
                    if (addr.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                        return addr.ToString();
                }

                return addresses.Length > 0 ? addresses[0].ToString() : null;
            }
            catch
            {
                return null;
            }
        }

        private static IPAddress GetLocalAddressForRemote(string IP)
        {
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up)
                    continue;

                var ipProps = ni.GetIPProperties();

                foreach (var ua in ipProps.UnicastAddresses)
                {
                    if (ua.Address.AddressFamily != AddressFamily.InterNetwork)
                        continue;

                    byte[] mask = ua.IPv4Mask.GetAddressBytes();
                    byte[] local = ua.Address.GetAddressBytes();
                    byte[] remote = IPAddress.Parse(IP).GetAddressBytes();

                    bool sameSubnet = true;

                    for (int i = 0; i < 4; i++)
                    {
                        if ((local[i] & mask[i]) != (remote[i] & mask[i]))
                        {
                            sameSubnet = false;
                            break;
                        }
                    }

                    if (sameSubnet)
                        return ua.Address;
                }
            }

            return null;
        }

        private static void Help(string message)
        {

            Log(""); Log("");
            Log("    |\\__/|", true);
            Log("    ( - -)", true);
            Log("   / >[+XX] SOFTPHONE v0.1", true);
            Log("   ", true);
            Console.WriteLine("   Usage: Softphone.exe --sip-server <addr|domain> --sip-user <usr> --sip-pwd <pwd> --callnum <num>");
            Console.WriteLine("          OPTIONAL : (--mic|--audio-file), --rec, --verbose");
            Console.WriteLine("                                                          ");
            Console.WriteLine("   --sip-server, SIP Server IP addr or domain");
            Console.WriteLine("   --sip-user,   SIP username");
            Console.WriteLine("   --sip-pwd,    SIP password");
            Console.WriteLine("   --callnum,    Phone number to call");
            Console.WriteLine("   --mic,        Use microphone");
            Console.WriteLine("   --audio-file, Use WAV/MP3 file (no microphone)");
            Console.WriteLine("   --rec,        Record the call");
            Console.WriteLine("   --verbose,    Show more messages");
            Console.WriteLine($"{System.Environment.NewLine}{message}{System.Environment.NewLine}");
            Environment.Exit(1);
        }

        private static void Information(string message)
        {
            if (Verbose)
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Log(message);
                Console.ResetColor();
            }
        }

        private static void DrawHeader()
        {
            Console.SetCursorPosition(0, 0);
            Console.BackgroundColor = ConsoleColor.DarkYellow;
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(" <Press ENTER to HangUp> ".PadRight(Console.WindowWidth, ' '));
            Console.ResetColor();

        }

        private static void Log(string message, bool isheader = false)
        {
            lock (consoleLock)
            {

                Console.SetCursorPosition(0, Console.CursorTop);

                if (Console.CursorTop < logStartLine)
                    Console.SetCursorPosition(0, logStartLine);

                if (isheader)
                {
                    Console.Write($"\x1b[93m{message}\x1b[0m\n");
                }
                else
                {
                    Console.WriteLine(message);
                }

            }
        }

        private static void CleanAfterCall()
        {
            if (Record)
            {

                if (null != wavRemote)
                {
                    wavRemote?.Dispose();
                    wavRemote = null;
                }

                if (null != wavLocal)
                {
                    wavLocal?.Dispose();
                    wavLocal = null;
                }

                if (null != waveOut)
                {
                    waveOut?.Dispose();
                    waveOut = null;
                }

                if (null != waveProvider)
                {
                    waveProvider = null;
                }

                if (null != wavStereo)
                {
                    wavStereo?.Dispose();
                    wavStereo = null;
                }

                Information("[REC] Enregistrement terminé.");

            }

            if (UseMic)
            {
                if (null != micCapture)
                {
                    micCapture.StopRecording();
                    micCapture?.Dispose();
                    micCapture = null;
                }

                Information("[MIC] terminé.");
            }

            if (null != regUserAgent)
            {
                Information("[X] regUserAgent stopped and free");
                if (regUserAgent.IsRegistered)
                {
                    regUserAgent.Stop();
                }
                regUserAgent = null;
            }

            if (null != callDescriptor)
            {
                Information("[X] callDescriptor free");
                callDescriptor = null;
            }

            if (null != rtpChannel)
            {
                Information("[X] rtpChannel stopped and free");
                if (!rtpChannel.IsClosed)
                {
                    rtpChannel.Close("End call");
                }
                rtpChannel?.Dispose();
                rtpChannel = null;
            }

            if (null != uac)
            {
                Information("[X] uac free");
                uac = null;
            }

            if (null != sipTransport)
            {
                Information("[X] sipTransport.Shutdown");
                sipTransport.Shutdown();
                sipTransport?.Dispose();
                sipTransport = null;
            }

        }
    }
}
