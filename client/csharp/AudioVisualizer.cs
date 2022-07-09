using System.Numerics;
using Accord.Math;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace ArduinoArgb
{
    public class AudioVisualizer : IDisposable
    {
        private static readonly int[] DefaultBand = { 63, 128, 260, 600, 1100, 2100, 4000, 8000 };

        private const int SampleSize = 4096;
        private const int SampleBytes = 4;
        private readonly RgbDevice _device;

        private readonly int[] _band;
        private readonly int[,] _queue;

        private BufferedWaveProvider _bwp;
        private MMDeviceCollection _devices;
        private IWaveIn _waveIn;

        private int[] _spectrum;
        private bool _enabled;
        private int _speakerIndex;
        private int _qIndex;
        private int _lastDeviceSelect = -1;

        public AudioVisualizer(RgbDevice device)
        {
            _device = device;
            _queue = new int[MatrixPanel.Width, SampleBytes];
            _band = new int[MatrixPanel.Width];

            RefreshDeviceList();
        }

        private async Task WriteLoop()
        {
            while (_enabled)
            {
                await WriteSpectrumAsync();
            }
        }

        private async Task SpectrumLoop()
        {
            while (_enabled)
            {
                ThreadPool.QueueUserWorkItem(_ =>
                {
                    lock (this)
                    {
                        RefreshDeviceList();
                    }
                });
                    

                UpdateSpectrum();
                await Task.Delay(200);
            }
        }

        private async Task WriteSpectrumAsync()
        {
            if (_spectrum == null)
            {
                await Task.Delay(100);
                return;
            }

            var modifiers = new[] { 36, 34, 32, 28, 24, 21, 19, 18 };

            for (byte x = 0; x < MatrixPanel.Width; x++)
            {
                var height = (byte)(_spectrum[x] * modifiers[x] / MatrixPanel.Height);

                for (byte y = 0; y < MatrixPanel.Height; y++)
                {
                    var color = y < height
                        ? new Color24(0, 0, 255) // Color24.FromHsl((double)x / (MatrixPanel.Width - 1), 0.5d, 0.5d)
                        : Color24.Black;

                    await _device.SetPixelAsync(x, y, color);
                }
            }
        }

        public void Dispose()
        {
            Stop();
        }

        private void RefreshDeviceList()
        {
            var mmdeviceEnumerator = new MMDeviceEnumerator();
            if (!mmdeviceEnumerator.HasDefaultAudioEndpoint(DataFlow.Render, Role.Console))
            {
                return;
            }

            var defaultAudioEndpoint = mmdeviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Console);
            _devices = mmdeviceEnumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);

            var idx = 0;
            foreach (var mmdevice in _devices)
            {
                if (mmdevice.ID == defaultAudioEndpoint.ID)
                {
                    _speakerIndex = idx;
                }

                idx++;
            }
        }

        private void RecalculateBandIndex()
        {
            for (var i = 0; i < MatrixPanel.Width; i++)
            {
                _band[i] = DefaultBand[i] * SampleSize / (_waveIn.WaveFormat.SampleRate * (SampleBytes/2));
            }
        }

        private void InitWasapiLoopback(int index)
        {
            if (_devices.Count == 0 || _lastDeviceSelect == index)
            {
                return;
            }

            _lastDeviceSelect = index;
            _waveIn = new WasapiLoopbackCapture(_devices[index]);
            _waveIn.DataAvailable += OnSampleDataAvailable;
            
            _bwp = new BufferedWaveProvider(_waveIn.WaveFormat)
            {
                BufferLength = SampleSize * SampleBytes,
                DiscardOnBufferOverflow = true
            };

            RecalculateBandIndex();
        }

        private void OnSampleDataAvailable(object sender, WaveInEventArgs e)
        {
            _bwp.AddSamples(e.Buffer, 0, SampleSize * SampleBytes);
        }

        private void UpdateSpectrum()
        {
            var samples = new byte[SampleSize * SampleBytes];
            _bwp.Read(samples, 0, samples.Length);

            if (samples.Length == 0)
            {
                _spectrum = null;
                return;
            }

            var data = new double[samples.Length / SampleBytes];
            for (var i = 0; i < data.Length; i++)
            {
                data[i] = (samples[i * 4 + 3] << 24) | (samples[i * 4 + 2] << 16) |
                          (samples[i * 4 + 1] << 8) | samples[i * 4];
            }

            var fft = Fft(data);
            var band = new int[MatrixPanel.Width];
            for (var x = 0; x < band.Length; x++)
            {
                _queue[x, _qIndex] = (int)fft[_band[x]];
                var bandHeight = 0;
                for (var l = 0; l < SampleBytes; l++)
                {
                    bandHeight += _queue[x, l];
                }

                bandHeight /= 4;
                bandHeight -= 300;

                if (bandHeight < 0)
                {
                    band[x] = 0;
                }
                else
                {
                    band[x] = bandHeight / 16;
                }
            }

            _qIndex++;
            _qIndex &= 0x3;
            _spectrum = band;
        }

        private double[] Fft(double[] data)
        {
            var transformedData = new double[data.Length];
            var fft = data
                .Select(x => (Complex)x)
                .ToArray();

            FourierTransform.FFT(fft, FourierTransform.Direction.Forward);
            for (var j = 0; j < MatrixPanel.Width; j++)
            {
                var band = _band[j];
                transformedData[band] = fft[band].Magnitude;
                transformedData[band] += 10.0;
                transformedData[band] = Math.Log10(transformedData[band]) * 50.0;
            }

            return transformedData;
        }

        public void Start()
        {
            if (_devices.Count == 0)
            {
                RefreshDeviceList();
                return;
            }

            if (!_enabled)
            {
                InitWasapiLoopback(_speakerIndex);
                _waveIn.StartRecording();

                _enabled = true;
                Task.Run(SpectrumLoop);
                Task.Run(WriteLoop);
            }
        }

        public void Stop()
        {
            if (!_enabled || _devices.Count == 0)
            {
                return;
            }

            _enabled = false;

            _waveIn?.StopRecording();

            for (var i = 0; i < MatrixPanel.Width; i++)
            {
                for (var j = 0; j < SampleBytes; j++)
                {
                    _queue[i, j] = 0;
                }
            }
        }

        public void ChangeSpeakerLoopback(int deviceId)
        {
            if (_devices.Count == 0)
            {
                return;
            }

            Stop();
            _speakerIndex = deviceId;

            InitWasapiLoopback(_speakerIndex);

            if (_enabled)
            {
                Start();
            }
        }
    }
}