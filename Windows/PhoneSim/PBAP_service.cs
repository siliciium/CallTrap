/*
Copyright 2026 Silicium

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

    http://www.apache.org/licenses/LICENSE-2.0
*/
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Rfcomm;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;


/*
Service Name: Phonebook Access Server
Service Description: Simulated PBAP Phone
Service Provider: PhoneSim
Service RecHandle: 0x10078
Service Class ID List:
  UUID 128: 0000112f-0000-1000-8000-00805f9b34fb
Protocol Descriptor List:
  "L2CAP" (0x0100)
  "RFCOMM" (0x0003)
    Channel: 4
Profile Descriptor List:
  "Phonebook Access - PSE" (0x112f)
    Version: 0x0102
*/


namespace PhoneSim
{
    public class PbapAppParams
    {
        public ushort? MaxListCount { get; set; }
        public ushort? ListStartOffset { get; set; }
        public byte? Order { get; set; }
        public string SearchValue { get; set; }
        public byte? SearchProperty { get; set; }
        public byte? Format { get; set; }
        public uint? PhonebookSize { get; set; }
        public byte? NewMissedCalls { get; set; }
        public ulong? VCardSelector { get; set; }
        public byte? VCardSelectorOperator { get; set; }
        public string Handle { get; set; }
    }

    public class Contact
    {
        public string Name { get; set; }
        public string Number { get; set; }
    }

    internal class PBAP_service
    {
        private RfcommServiceProvider provider_PBAP;
        private StreamSocketListener listener_PBAP;
        public string local_btaddr { get; set; }
        public string local_btport { get; set; }
        public string remote_btaddr { get; set; }
        private string currentPath = "/";

        private readonly BluetoothAdapter adapter;
        private static RichTextBox tb_pbap;
        public event Action TitleRequested;

        public Brush brush_Rx { get; set; }
        public Brush brush_Tx { get; set; }


        public PBAP_service(BluetoothAdapter _adapter, RichTextBox _tb_pbap)
        {
            adapter = _adapter ?? throw new ArgumentNullException(nameof(_adapter));
            tb_pbap = _tb_pbap ?? throw new ArgumentNullException(nameof(_tb_pbap));
        }

        public async Task LoadAsync()
        {
            try
            {

                Guid serviceUuid = new Guid("0000112F-0000-1000-8000-00805F9B34FB");
                provider_PBAP = await RfcommServiceProvider
                    .CreateAsync(RfcommServiceId.FromUuid(serviceUuid))
                    .AsTask();

                Debug.WriteLine(provider_PBAP);
                listener_PBAP = new StreamSocketListener();
                listener_PBAP.ConnectionReceived += OnConnectionReceived;

                await listener_PBAP.BindServiceNameAsync(provider_PBAP.ServiceId.AsString(), SocketProtectionLevel.PlainSocket).AsTask();
                Debug.WriteLine(listener_PBAP.Information.LocalPort);
                setCustomAttributes(provider_PBAP);

                local_btaddr = FormatBluetoothAddress(adapter.BluetoothAddress);
                local_btport = listener_PBAP.Information.LocalPort;

                Debug.WriteLine($"Listening on {local_btaddr} RFCOMM port {local_btport}");


                // 4. SDP Publish
                provider_PBAP.StartAdvertising(listener_PBAP, true);

            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex); ;
            }

        }

        private static string FormatBluetoothAddress(ulong address)
        {
            return string.Join(":",
                Enumerable.Range(0, 6)
                    .Select(i => ((address >> (8 * (5 - i))) & 0xFF).ToString("X2"))
            );
        }

        private static void LogHexAscii(byte[] buffer, int length)
        {
            if (length <= 0)
            {
                Debug.WriteLine("[OBEX] <no data>");
                _AppendText(tb_pbap, $"[OBEX] <no data>", Brushes.DarkSlateGray);
                return;
            }

            const int bytesPerLine = 16;

            for (int i = 0; i < length; i += bytesPerLine)
            {
                int lineLength = Math.Min(bytesPerLine, length - i);

                // HEX
                StringBuilder hex = new StringBuilder();
                for (int j = 0; j < lineLength; j++)
                    hex.Append(buffer[i + j].ToString("X2")).Append(" ");

                // ASCII
                StringBuilder ascii = new StringBuilder();
                for (int j = 0; j < lineLength; j++)
                {
                    byte b = buffer[i + j];
                    ascii.Append(b >= 32 && b <= 126 ? (char)b : '.');
                }

                Debug.WriteLine($"{i:X4}  {hex.ToString().PadRight(48)}  {ascii}");
                _AppendText(tb_pbap, $"{i:X4}  {hex.ToString().PadRight(48)}  {ascii}", Brushes.DarkSlateGray);              
            }

            _AppendText(tb_pbap, "\n", Brushes.DarkSlateGray);
        }

        private static void AddSdpString(RfcommServiceProvider provider, ushort id, string value)
        {
            var writer = new DataWriter();
            writer.UnicodeEncoding = Windows.Storage.Streams.UnicodeEncoding.Utf8;

            writer.WriteByte(0x25); // TextString
            writer.WriteByte((byte)value.Length);
            writer.WriteString(value);

            provider.SdpRawAttributes.Add(id, writer.DetachBuffer());
        }

        private static void AddSdpUInt8(RfcommServiceProvider provider, ushort attributeId, byte value)
        {
            var writer = new DataWriter();

            // DataElement: UINT8
            // 0x08 = Unsigned Integer, 1 byte
            writer.WriteByte(0x08);
            writer.WriteByte(value);

            provider.SdpRawAttributes.Add(attributeId, writer.DetachBuffer());
        }

        private static void AddSdpUInt16(RfcommServiceProvider provider, ushort id, ushort value)
        {
            var writer = new DataWriter();
            writer.WriteByte(0x09); // UInt16
            writer.WriteUInt16(value);

            provider.SdpRawAttributes.Add(id, writer.DetachBuffer());
        }

        private static void setCustomAttributes(RfcommServiceProvider provider)
        {
            //
            // --- Complete SDP of a Phonebook Access Server (PBAP PSE) ---
            //

            // 0x0100 – Service Name
            AddSdpString(provider, 0x0100, "Phonebook Access Server");

            // 0x0101 – Service Description
            AddSdpString(provider, 0x0101, "Simulated PBAP Phone");

            // 0x0102 – Provider Name
            AddSdpString(provider, 0x0102, "PhoneSim");

            //
            // PBAP-specific attributes
            //

            // 0x0314 – Supported Repositories
            AddSdpUInt8(provider, 0x0314, 0x03); // local + SIM

            // 0x0315 – Supported Features
            AddSdpUInt16(provider, 0x0315, 0x007F); // toutes features activées

            //
            // Profile Descriptor List (PBAP 1.2)
            //
            {
                var writer = new DataWriter();

                writer.WriteByte(0x35); // Sequence
                writer.WriteByte(0x08); // Length

                writer.WriteByte(0x35); writer.WriteByte(0x06); // Sequence
                writer.WriteByte(0x19); writer.WriteUInt16(0x112F); // PBAP PSE UUID
                writer.WriteByte(0x09); writer.WriteUInt16(0x0102); // Version 1.2

                provider.SdpRawAttributes.Add(0x0009, writer.DetachBuffer());
            }

            //
            // IMPORTANT: Do not add 0x0001
            // Windows generates it automatically
            //
        }

        private async void OnConnectionReceived(StreamSocketListener sender, StreamSocketListenerConnectionReceivedEventArgs args)
        {
            var socket = args.Socket;
            var input = socket.InputStream.AsStreamForRead();
            var output = socket.OutputStream.AsStreamForWrite();

            remote_btaddr = socket.Information.RemoteAddress.RawName.Replace("(", "").Replace(")","");
            Debug.WriteLine($"- client: {remote_btaddr}");
            _AppendText(tb_pbap, $"{System.Environment.NewLine}◎ CONNECTED: {remote_btaddr}{System.Environment.NewLine}", Brushes.LightGreen);

            UpdateTitle();

            await HandleObexSession(input, output);
        }

        private async Task HandleObexSession(Stream input, Stream output)
        {
            while (true)
            {
                // Read OBEX header (opcode + length)
                byte[] header = new byte[3];
                int read = await input.ReadAsync(header, 0, 3);

                if (read == 0)
                {
                    Debug.WriteLine("Client disconnected");

                    _AppendText(tb_pbap, $"◎ DISCONNECTED: {remote_btaddr}", Brushes.IndianRed);

                    remote_btaddr = String.Empty;
                    UpdateTitle();
                    currentPath = "/";
                    return;
                }

                byte opcode = header[0];
                int packetLength = (header[1] << 8) | header[2];

                // Read the rest of the packet
                byte[] payload = new byte[packetLength - 3];
                int read_payload = await input.ReadAsync(payload, 0, payload.Length);

                Debug.WriteLine("> [Rx]");                

                //
                // OBEX ROUTING
                //
                switch (opcode)
                {
                    case 0x80: // CONNECT
                        _AppendText(tb_pbap, "[Rx] OBEX : CONNECT", brush_Rx);
                        LogHexAscii(header.Concat(payload).ToArray(), packetLength);
                        await SendObexConnectResponse(output);
                        break;

                    case 0x85: // SETPATH
                        _AppendText(tb_pbap, "[Rx] OBEX : SETPATH", brush_Rx);
                        LogHexAscii(header.Concat(payload).ToArray(), packetLength);
                        await HandleObexSetPath(header.Concat(payload).ToArray(), output);
                        break;

                    case 0x82: // GET                        
                    case 0x83: // GET (Final)
                        _AppendText(tb_pbap, "[Rx] OBEX : GET", brush_Rx);
                        LogHexAscii(header.Concat(payload).ToArray(), packetLength);
                        await HandleObexGet(header.Concat(payload).ToArray(), output);
                        break;

                    case 0x01: // PUT
                    case 0x81: // PUT Final + SRM
                        _AppendText(tb_pbap, "[Rx] OBEX : PUT", brush_Rx);
                        LogHexAscii(header.Concat(payload).ToArray(), packetLength);
                        await HandleObexPut(header.Concat(payload).ToArray(), output);
                        break;

                    default:
                        _AppendText(tb_pbap, $"[Rx] OBEX : Unknown OBEX opcode: {opcode:X2}", brush_Rx);
                        LogHexAscii(header.Concat(payload).ToArray(), packetLength);
                        Debug.WriteLine($"Unknown OBEX opcode: {opcode:X2}");
                        await SendObexError(output);
                        break;
                }
            }
        }

        private async Task SendObexConnectResponse(Stream output)
        {
            Debug.WriteLine("[SendObexConnectResponse]");

            byte[] resp = new byte[]
            {
                0xA0, 0x00, 0x07, 0x10, 0x00, 0xFF, 0xFF
            };

            _AppendText(tb_pbap, "[Tx] OBEX : Success", brush_Tx);
            LogHexAscii(resp, resp.Length);

            await output.WriteAsync(resp, 0, resp.Length);
            await output.FlushAsync();
        }

        private async Task SendObexSetPathSuccess(Stream output)
        {
            byte[] resp = new byte[]
            {
                0xA0, 0x00, 0x03
            };

            _AppendText(tb_pbap, "[Tx] OBEX : Success", brush_Tx);
            LogHexAscii(resp, resp.Length);

            await output.WriteAsync(resp, 0, resp.Length);
            await output.FlushAsync();
        }

        private async Task HandleObexPut(byte[] buffer, Stream output)
        {
            // 0x81 = PUT Final (used here just for SRM)
            byte opcode = (byte)(buffer[0] & 0x7F);
            if (opcode == 0x01) // PUT
            {
                byte[] resp = { 0xA0, 0x00, 0x03 }; // Success, length=3
                
                _AppendText(tb_pbap, "[Tx] OBEX : Success", brush_Tx);
                LogHexAscii(resp, resp.Length);

                await output.WriteAsync(resp, 0, resp.Length);
                await output.FlushAsync();
            }
        }

        private async Task HandleObexGet(byte[] buffer, Stream output)
        {
            DumpObexHeaders(buffer);

            int length = buffer.Length;

            string type = ExtractObexType(buffer, length);
            string name = ExtractObexName(buffer, length);

            Debug.WriteLine($"type:{type} name:{name ?? "<none>"}");


            // Base: current PBAP directory (e.g., "telecom/pb")
            string basePath = PbapFileSystem.ResolvePbapPath(currentPath);



            // Le listing XML PBAP
            if (type == "x-bt/vcard-listing")
            {
                Debug.WriteLine("[SendVcardListing]");

                PbapAppParams app = ExtractAppParams(buffer, length);

                // For a listing, we always work on a folder
                // If Name is present and represents a subfolder, you can use it,
                // but in practice BlueZ just sends SETPATH ​​then GET without Name.
                string listingPath = basePath;


                await SendVcardListing(output, listingPath, app);
                return;
            }

            // The complete file phonebook.vcf
            else if (type == "x-bt/phonebook")
            {
                Debug.WriteLine("[SendPhonebook]");

                PbapAppParams app = ExtractAppParams(buffer, length);

                string phonebookPath;

                if (!string.IsNullOrEmpty(name))
                {
                    // Here, BlueZ often sends "telecom/pb.vcf" → full path
                    phonebookPath = PbapFileSystem.ResolvePbapPath(name);
                }
                else
                {
                    // Otherwise: currentPath points to "telecom" or "telecom/pb"
                    // and we add pb.vcf
                    phonebookPath = Path.Combine(basePath, "pb.vcf");
                }

                await SendPhonebook(output, phonebookPath, app);

                return;
            }

            // An individual vCard
            else if (type == "x-bt/vcard")
            {
                Debug.WriteLine("[SendVcard]");

                PbapAppParams app = ExtractAppParams(buffer, length);

                string vcardPath;

                if (!string.IsNullOrEmpty(name))
                {
                    // Here, Name is a relative handle: "1.vcf", "42.vcf", etc.
                    // It is resolved in the current directory (telecom/pb)
                    vcardPath = Path.Combine(basePath, name);
                }
                else
                {
                    // More unusual case: no Name, currentPath already points to a file
                    // or you can choose to return an error
                    vcardPath = basePath;
                }

                await SendVcardEntry(output, vcardPath, app);

                return;
            }

            await SendObexError(output);
        }

        private async Task HandleObexSetPath(byte[] buffer, Stream output)
        {
            Debug.WriteLine("[HandleObexSetPath]");

            int length = buffer.Length;

            // Flags OBEX
            byte flags = buffer[3];
            bool goUp = (flags & 0x01) != 0;
            bool dontCreate = (flags & 0x02) != 0;

            // Extract header Name (Unicode)
            string name = ExtractObexName(buffer, length); // "" si absent

            if (goUp)
            {
                // Up one level
                int idx = currentPath.LastIndexOf('/');
                if (idx > 0)
                    currentPath = currentPath.Substring(0, idx);
                else
                    currentPath = "/";
            }
            else if (!string.IsNullOrEmpty(name))
            {
                // Down in sub folder
                if (currentPath == "/")
                    currentPath = name;
                else
                    currentPath = currentPath + "/" + name;
            }

            Debug.WriteLine($"[SETPATH] {currentPath}");

            string diskPath = PbapFileSystem.ResolvePbapPath(currentPath);

            if (!Directory.Exists(diskPath))
            {
                Debug.WriteLine($"[SETPATH] Path not found: {diskPath}");
                await SendObexErrorNotFound(output);
                return;
            }

            await SendObexSetPathSuccess(output);
        }

        private byte[] BuildAppParamsPhonebookSize(ushort size)
        {
            return new byte[]
            {
                0x4C, 0x00, 0x07,   // Header 0x4C, length = 7 bytes
                0x08, 0x02,         // Tag 0x08 (PhonebookSize), length 2
                (byte)(size >> 8),
                (byte)(size & 0xFF)
            };
        }

        private async Task SendPhonebook(Stream output, string diskPath, PbapAppParams app)
        {
            int max = app.MaxListCount ?? 65535;
            int offset = app.ListStartOffset ?? 0;

            Debug.WriteLine($"[SendPhonebook] max:{max} offset:{offset}");
            Debug.WriteLine($"[SendPhonebook] diskPath={diskPath}");

            //
            // 1) Determine what file to read : pb.vcf
            //
            if (!File.Exists(diskPath))
            {
                Debug.WriteLine($"[SendPhonebook] {diskPath} introuvable");
                await SendObexErrorNotFound(output);
                return;
            }

            //
            // 2) Load all lines
            //
            string[] allLines = File.ReadAllLines(diskPath);

            //
            // 3) Cut to individual contact
            //
            List<string> contacts = SplitVcards(allLines);

            //
            // 4) Apply offset + max
            //
            var selected = contacts
                .Skip(offset)
                .Take(max)
                .ToList();

            //
            // 5) Build final content
            //
            StringBuilder sb = new StringBuilder();
            foreach (var v in selected)
                sb.AppendLine(v);

            byte[] body = Encoding.UTF8.GetBytes(sb.ToString());

            //
            // 6) Header Application Parameters (0x4C)
            //
            ushort phonebookSize = (ushort)contacts.Count;
            byte[] appParams = BuildAppParamsPhonebookSize(phonebookSize);

            //
            // 7) Header Body (0x48)
            //
            byte[] bodyHeader = new byte[3 + body.Length];
            bodyHeader[0] = 0x48; // Body
            bodyHeader[1] = (byte)((body.Length + 3) >> 8);
            bodyHeader[2] = (byte)((body.Length + 3) & 0xFF);
            System.Buffer.BlockCopy(body, 0, bodyHeader, 3, body.Length);

            //
            // 8) End-of-body (0x49)
            //
            byte[] endBody = { 0x49, 0x00, 0x03 };

            //
            // 9) Total Lenght OBEX
            //
            int totalLength =
                3 +
                appParams.Length +
                bodyHeader.Length +
                endBody.Length;

            byte[] response = new byte[totalLength];
            response[0] = 0xA0; // Success
            response[1] = (byte)(totalLength >> 8);
            response[2] = (byte)(totalLength & 0xFF);

            int pos = 3;

            System.Buffer.BlockCopy(appParams, 0, response, pos, appParams.Length);
            pos += appParams.Length;

            System.Buffer.BlockCopy(bodyHeader, 0, response, pos, bodyHeader.Length);
            pos += bodyHeader.Length;

            System.Buffer.BlockCopy(endBody, 0, response, pos, endBody.Length);

            _AppendText(tb_pbap, "[Tx] OBEX : x-bt/phonebook", brush_Tx);
            LogHexAscii(response, response.Length);

            await output.WriteAsync(response, 0, response.Length);
            await output.FlushAsync();
        }

        private List<string> SplitVcards(string[] lines)
        {
            var list = new List<string>();
            var current = new List<string>();

            foreach (var line in lines)
            {
                if (line.StartsWith("BEGIN:VCARD"))
                {
                    current = new List<string>();
                }

                current.Add(line);

                if (line.StartsWith("END:VCARD"))
                {
                    string vcard = string.Join("\r\n", current.Where(l => !string.IsNullOrWhiteSpace(l)));
                    list.Add(vcard);
                }
            }

            return list;
        }

        public static string BuildVCard(Contact c)
        {
            return
        $@"BEGIN:VCARD
VERSION:3.0
FN:{c.Name}
TEL;CELL:{c.Number}
END:VCARD";
        }

        private async Task SendVcardListing(Stream output, string diskPath, PbapAppParams app)
        {
            int max = app.MaxListCount ?? 65535;
            int offset = app.ListStartOffset ?? 0;

            Debug.WriteLine($"[SendVcardListing] max:{max} offset:{offset}");
            Debug.WriteLine($"[SendVcardListing] diskPath={diskPath}");

            //
            // 1) Verify id folder exists
            //
            if (!Directory.Exists(diskPath))
            {
                Debug.WriteLine("[SendVcardListing] Dossier introuvable");
                byte[] err = { 0xC0, 0x00, 0x03 };
                await output.WriteAsync(err, 0, err.Length);
                return;
            }

            //
            // 2) Enumerate .vcf files in current folder
            //
            string[] files = Directory.GetFiles(diskPath, "*.vcf");

            // Example : "1.vcf", "2.vcf", "42.vcf"
            var entries = files
                .Select(f => new FileInfo(f))
                .OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            int total = entries.Count;

            //
            // 3) Do offset + max
            //
            var selected = entries
                .Skip(offset)
                .Take(max)
                .ToList();

            //
            // 4) Build vcard-listing.xml
            //
            StringBuilder xml = new StringBuilder();
            xml.AppendLine("<?xml version=\"1.0\"?>");
            xml.AppendLine("<vCard-listing version=\"1.0\">");

            foreach (var f in selected)
            {
                string name = Path.GetFileName(f.Name); // ex: "42.vcf"

                string fullPath = f.FullName;
                string fn = File.ReadLines(fullPath).FirstOrDefault(l => l.StartsWith("FN:"))?.Substring(3) ?? name; // fallback

                xml.AppendLine($"  <card handle=\"{name}\" name=\"{fn}\"/>");
            }

            xml.AppendLine("</vCard-listing>");

            byte[] body = Encoding.UTF8.GetBytes(xml.ToString());

            //
            // 5) Header Application Parameters (PhonebookSize)
            //
            ushort phonebookSize = (ushort)total;
            byte[] appParams = BuildAppParamsPhonebookSize(phonebookSize);

            //
            // 6) Header Body (0x48)
            //
            byte[] bodyHeader = new byte[3 + body.Length];
            bodyHeader[0] = 0x48;
            bodyHeader[1] = (byte)((body.Length + 3) >> 8);
            bodyHeader[2] = (byte)((body.Length + 3) & 0xFF);
            System.Buffer.BlockCopy(body, 0, bodyHeader, 3, body.Length);

            //
            // 7) End-of-body (0x49)
            //
            byte[] endBody = { 0x49, 0x00, 0x03 };

            //
            // 8) Total Length OBEX
            //
            int totalLength =
                3 +
                appParams.Length +
                bodyHeader.Length +
                endBody.Length;

            byte[] response = new byte[totalLength];
            response[0] = 0xA0; // Success
            response[1] = (byte)(totalLength >> 8);
            response[2] = (byte)(totalLength & 0xFF);

            int pos = 3;

            System.Buffer.BlockCopy(appParams, 0, response, pos, appParams.Length);
            pos += appParams.Length;

            System.Buffer.BlockCopy(bodyHeader, 0, response, pos, bodyHeader.Length);
            pos += bodyHeader.Length;

            System.Buffer.BlockCopy(endBody, 0, response, pos, endBody.Length);

            _AppendText(tb_pbap, "[Tx] OBEX : x-bt/vcard-listing", brush_Tx);
            LogHexAscii(response, response.Length);

            await output.WriteAsync(response, 0, response.Length);
            await output.FlushAsync();
        }

        private async Task SendVcardEntry(Stream output, string diskPath, PbapAppParams app)
        {
            Debug.WriteLine($"[SendVcardEntry] diskPath={diskPath}");

            if (!File.Exists(diskPath))
            {
                Debug.WriteLine($"[SendVcardEntry] {diskPath} introuvable");
                await SendObexErrorNotFound(output);
                return;
            }

            //
            // 2) Read .vcf file
            //
            string content = File.ReadAllText(diskPath);
            byte[] body = Encoding.UTF8.GetBytes(content);

            //
            // 3) Header Body (0x48)
            //
            byte[] bodyHeader = new byte[3 + body.Length];
            bodyHeader[0] = 0x48; // Body
            bodyHeader[1] = (byte)((body.Length + 3) >> 8);
            bodyHeader[2] = (byte)((body.Length + 3) & 0xFF);
            System.Buffer.BlockCopy(body, 0, bodyHeader, 3, body.Length);

            //
            // 4) End-of-body (0x49)
            //
            byte[] endBody = { 0x49, 0x00, 0x03 };

            //
            // 5) Totel Length OBEX
            //
            int totalLength =
                3 +
                bodyHeader.Length +
                endBody.Length;

            byte[] response = new byte[totalLength];
            response[0] = 0xA0; // Success
            response[1] = (byte)(totalLength >> 8);
            response[2] = (byte)(totalLength & 0xFF);

            int pos = 3;

            System.Buffer.BlockCopy(bodyHeader, 0, response, pos, bodyHeader.Length);
            pos += bodyHeader.Length;

            System.Buffer.BlockCopy(endBody, 0, response, pos, endBody.Length);

            _AppendText(tb_pbap, "[Tx] OBEX : x-bt/vcard", brush_Tx);
            LogHexAscii(response, response.Length);

            await output.WriteAsync(response, 0, response.Length);
            await output.FlushAsync();
        }

        private async Task SendObexError(Stream output)
        {
            // OBEX error response: Bad Request
            byte[] resp = new byte[]
            {
                0xC0, // Response code: Bad Request
                0x00, 0x03 // Packet length = 3 bytes (header only)
            };

            _AppendText(tb_pbap, "[Tx] OBEX : Error", brush_Tx);
            LogHexAscii(resp, resp.Length);

            await output.WriteAsync(resp, 0, resp.Length);
            await output.FlushAsync();
        }

        private async Task SendObexErrorNotFound(Stream output)
        {
            byte[] resp = { 0xC4, 0x00, 0x03 };

            _AppendText(tb_pbap, "[Tx] OBEX : ErrorNotFound", brush_Tx);
            LogHexAscii(resp, resp.Length);

            await output.WriteAsync(resp, 0, resp.Length);
            await output.FlushAsync();
        }

        private string ExtractObexType(byte[] buffer, int length)
        {
            int index = 3; // Skip opcode + length (3 bytes)

            while (index < length)
            {
                byte headerId = buffer[index];

                if (headerId == 0x42) // TYPE header
                {
                    int headerLength = (buffer[index + 1] << 8) | buffer[index + 2];
                    var s = Encoding.ASCII.GetString(buffer, index + 3, headerLength - 3);
                    return s.TrimEnd('\0');
                }

                // Skip header
                if ((headerId & 0xC0) == 0x00) // Unicode
                {
                    int headerLength = (buffer[index + 1] << 8) | buffer[index + 2];
                    index += headerLength;
                }
                else if ((headerId & 0xC0) == 0x40) // Byte sequence
                {
                    int headerLength = (buffer[index + 1] << 8) | buffer[index + 2];
                    index += headerLength;
                }
                else if ((headerId & 0xC0) == 0x80) // 1 byte
                {
                    index += 2;
                }
                else if ((headerId & 0xC0) == 0xC0) // 4 bytes
                {
                    index += 5;
                }
            }

            return null;
        }

        private string ExtractObexName(byte[] buffer, int length)
        {
            int index = 3; // après opcode + length
            while (index < length)
            {
                byte headerId = buffer[index];

                // Header Name = 0x01
                if (headerId == 0x01)
                {
                    if (index + 2 >= length)
                        return "";

                    int headerLength = (buffer[index + 1] << 8) | buffer[index + 2];
                    int textLength = headerLength - 3;

                    if (textLength <= 0 || index + headerLength > length)
                        return "";

                    byte[] utf16 = new byte[textLength];
                    Array.Copy(buffer, index + 3, utf16, 0, textLength);

                    // UTF‑16BE → string
                    string name = Encoding.BigEndianUnicode.GetString(utf16);

                    // enlever le \0 final éventuel
                    return name.TrimEnd('\0');
                }

                // Header non Name → sauter
                if (headerId >= 0x00 && headerId <= 0x7F)
                {
                    // headers à longueur fixe → 1 octet
                    index += 1;
                }
                else
                {
                    // headers à longueur variable → lire la longueur
                    if (index + 2 >= length)
                        break;

                    int headerLength = (buffer[index + 1] << 8) | buffer[index + 2];
                    index += headerLength;
                }
            }

            return "";
        }

        private void DumpObexHeaders(byte[] buffer)
        {
            int index = 3;

            while (index < buffer.Length)
            {
                byte id = buffer[index];

                if ((id & 0xC0) == 0x00) // Unicode
                {
                    int len = (buffer[index + 1] << 8) | buffer[index + 2];
                    Debug.WriteLine($"Header Unicode 0x{id:X2} len={len}");
                    index += len;
                }
                else if ((id & 0xC0) == 0x40) // Byte sequence
                {
                    int len = (buffer[index + 1] << 8) | buffer[index + 2];
                    Debug.WriteLine($"Header ByteSeq 0x{id:X2} len={len}");
                    index += len;
                }
                else if ((id & 0xC0) == 0x80) // 1 byte
                {
                    Debug.WriteLine($"Header UInt8 0x{id:X2}");
                    index += 2;
                }
                else if ((id & 0xC0) == 0xC0) // 4 bytes
                {
                    Debug.WriteLine($"Header UInt32 0x{id:X2}");
                    index += 5;
                }
                else break;
            }
        }

        private int GetObexHeaderLength(byte[] buffer, int index)
        {
            byte id = buffer[index];

            // Unicode text or byte sequence → length is encoded in next 2 bytes
            if ((id & 0xC0) == 0x00 || (id & 0xC0) == 0x40)
            {
                // 2-byte big-endian length
                return (buffer[index + 1] << 8) | buffer[index + 2];
            }

            // 1-byte quantity
            if ((id & 0xC0) == 0x80)
            {
                return 2; // id + 1 byte
            }

            // 4-byte quantity
            if ((id & 0xC0) == 0xC0)
            {
                return 5; // id + 4 bytes
            }

            // Unknown header → stop parsing
            return 1;
        }

        private PbapAppParams ExtractAppParams(byte[] buffer, int length)
        {
            int index = 3;

            while (index < length)
            {
                byte id = buffer[index];

                if (id == 0x4C) // Application Parameters
                {
                    return ParseAppParams(buffer, index);
                }

                index += GetObexHeaderLength(buffer, index);
            }

            return new PbapAppParams();
        }

        public PbapAppParams ParseAppParams(byte[] buffer, int index)
        {
            PbapAppParams p = new PbapAppParams();

            // Header length
            int headerLength = (buffer[index + 1] << 8) | buffer[index + 2];
            int end = index + headerLength;

            // Move to first tag
            index += 3;

            while (index < end)
            {
                byte tag = buffer[index++];
                byte len = buffer[index++];

                switch (tag)
                {
                    case 0x01: // Order
                        p.Order = buffer[index];
                        break;

                    case 0x02: // SearchValue (string)
                        p.SearchValue = Encoding.UTF8.GetString(buffer, index, len);
                        break;

                    case 0x03: // SearchProperty
                        p.SearchProperty = buffer[index];
                        break;

                    case 0x04: // MaxListCount (2 bytes)
                        p.MaxListCount = (ushort)((buffer[index] << 8) | buffer[index + 1]);
                        break;

                    case 0x05: // ListStartOffset (2 bytes)
                        p.ListStartOffset = (ushort)((buffer[index] << 8) | buffer[index + 1]);
                        break;

                    case 0x06: // Format
                        p.Format = buffer[index];
                        break;

                    case 0x08: // PhonebookSize (2 bytes)
                        p.PhonebookSize = (ushort)((buffer[index] << 8) | buffer[index + 1]);
                        break;

                    case 0x09: // NewMissedCalls
                        p.NewMissedCalls = buffer[index];
                        break;

                    case 0x0A: // vCardSelector (8 bytes)
                        ulong selector = 0;
                        for (int i = 0; i < len; i++)
                            selector = (selector << 8) | buffer[index + i];
                        p.VCardSelector = selector;
                        break;

                    case 0x0B: // vCardSelectorOperator
                        p.VCardSelectorOperator = buffer[index];
                        break;

                    case 0x30: // ⭐ Handle (pour x-bt/vcard)
                        p.Handle = Encoding.ASCII.GetString(buffer, index, len);
                        break;

                    default:
                        break;
                }

                index += len;
            }

            return p;
        }

        public static void _AppendText(RichTextBox rtb, string text, Brush brush)
        {

            Application.Current.Dispatcher.Invoke(() =>
            {
                Paragraph p = new Paragraph
                {
                    Margin = new Thickness(0),
                    LineHeight = double.NaN,
                    TextAlignment = TextAlignment.Left
                };
                Run r = new Run(text)
                {
                    Foreground = brush
                };

                p.Inlines.Add(r);
                rtb.Document.Blocks.Add(p);

                rtb.ScrollToEnd();
            });
        }

        public static void AddRootPlanToRichTextBox(RichTextBox rtb, string plan)
        {
            rtb.Document.Blocks.Clear();

            var lines = plan.Split('\n');

            foreach (var rawLine in lines)
            {
                string line = rawLine.Replace("\r", "");

                Paragraph p = new Paragraph
                {
                    Margin = new Thickness(0),
                    LineHeight = double.NaN,
                    TextAlignment = TextAlignment.Left
                };


                int i = 0;

                while (i < line.Length)
                {
                    if ("│├└─|".Contains(line[i]))
                    {
                        p.Inlines.Add(new Run(line[i].ToString())
                        {
                            Foreground = Brushes.DarkSlateGray
                        });
                        i++;
                        continue;
                    }

                    if (line[i] == ' ')
                    {
                        p.Inlines.Add(new Run(" "));
                        i++;
                        continue;
                    }

                    int start = i;
                    while (i < line.Length && line[i] != ' ' && !"│├└─|".Contains(line[i]))
                        i++;

                    string token = line.Substring(start, i - start);

                    if (token.EndsWith("/"))
                    {
                        p.Inlines.Add(new Run(token)
                        {
                            Foreground = Brushes.Yellow
                        });
                    }
                    else if (token.EndsWith(".vcf"))
                    {
                        p.Inlines.Add(new Run(token)
                        {
                            Foreground = Brushes.DarkSlateGray
                        });
                    }
                    else
                    {
                        p.Inlines.Add(new Run(token)
                        {
                            Foreground = Brushes.DarkSlateGray
                        });
                    }
                }

                rtb.Document.Blocks.Add(p);
            }

            rtb.ScrollToEnd();
        }

        private void UpdateTitle()
        {
            TitleRequested?.Invoke();
        }



    }
}
