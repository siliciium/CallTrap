/*
Copyright 2026 Silicium

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

    http://www.apache.org/licenses/LICENSE-2.0
*/

using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace SoftPhone
{
    internal class RingTone
    {
        private static bool _running = false;
        private static Thread _thread;
        private static WaveOutEvent _waveOut;
        private static SignalGenerator _signal;

        public enum CountryTone
        {
            Perso,
            France,
            USA,
            UK,
            Germany,
            Japan,
            Australia
        }

        public static void StartRingTone(CountryTone country)
        {
            StopRingTone();
            _running = true;

            _thread = new Thread(() =>
            {
                while (_running)
                {
                    switch (country)
                    {
                        case CountryTone.Perso:
                            PlayTone(375, 1000);
                            SleepSafe(2000);
                            break;

                        case CountryTone.France:
                            PlayTone(440, 1000);
                            SleepSafe(4000);
                            break;

                        case CountryTone.USA:
                            PlayMixedTone(new[] { 440.0, 480.0 }, 2000);
                            SleepSafe(4000);
                            break;

                        case CountryTone.UK:
                            PlayTone(400, 400);
                            SleepSafe(200);
                            PlayTone(400, 400);
                            SleepSafe(2000);
                            break;

                        case CountryTone.Germany:
                            PlayTone(425, 1000);
                            SleepSafe(4000);
                            break;

                        case CountryTone.Japan:
                            PlayTone(400, 1000);
                            SleepSafe(2000);
                            break;

                        case CountryTone.Australia:
                            PlayTone(400, 400);
                            SleepSafe(200);
                            PlayTone(400, 400);
                            SleepSafe(2000);
                            break;
                    }
                }
            });

            _thread.IsBackground = true;
            _thread.Start();
        }

        private static void PlayTone(int freq, int durationMs)
        {
            if (!_running) return;

            _signal = new SignalGenerator()
            {
                Gain = 0.3,
                Frequency = freq,
                Type = SignalGeneratorType.Sin
            };

            _waveOut = new WaveOutEvent();
            _waveOut.Init(_signal);
            _waveOut.Play();

            SleepSafe(durationMs);

            _waveOut.Stop();
            _waveOut.Dispose();
        }

        private static void PlayMixedTone(double[] freqs, int durationMs)
        {
            if (!_running) return;

            var waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(44100, 1);

            var mixer = new MixingSampleProvider(waveFormat)
            {
                ReadFully = true
            };

            foreach (var f in freqs)
            {
                var osc = new SignalGenerator(waveFormat.SampleRate, waveFormat.Channels)
                {
                    Gain = 0.2,
                    Frequency = f,
                    Type = SignalGeneratorType.Sin
                };

                mixer.AddMixerInput(osc);
            }

            _waveOut = new WaveOutEvent();
            _waveOut.Init(mixer);
            _waveOut.Play();

            SleepSafe(durationMs);

            _waveOut.Stop();
            _waveOut.Dispose();
        }

        private static void SleepSafe(int ms)
        {
            int step = 10;
            for (int i = 0; i < ms / step && _running; i++)
                Thread.Sleep(step);
        }

        public static void StopRingTone()
        {
            _running = false;

            try { _waveOut?.Stop(); } catch { }
            try { _waveOut?.Dispose(); } catch { }

            if (_thread != null && _thread.IsAlive)
                _thread.Join(50);
        }
    }
}
