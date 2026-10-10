using System;
using System.Collections.Generic;

namespace CatLib.Assets;

internal static class JpegDecoder
{
    public const int MaxSide = 4096;

    internal static readonly byte[] ZigZag =
    {
        0, 1, 8, 16, 9, 2, 3, 10, 17, 24, 32, 25, 18, 11, 4, 5,
        12, 19, 26, 33, 40, 48, 41, 34, 27, 20, 13, 6, 7, 14, 21, 28,
        35, 42, 49, 56, 57, 50, 43, 36, 29, 22, 15, 23, 30, 37, 44, 51,
        58, 59, 52, 45, 38, 31, 39, 46, 53, 60, 61, 54, 47, 55, 62, 63
    };

    private static readonly float[] Cosines = BuildCosines();

    public static bool IsJpeg(byte[] data) => data != null && data.Length > 3 && data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF;

    public static bool TryDecode(byte[] data, out int width, out int height, out byte[] rgba, out string problem)
    {
        width = 0;
        height = 0;
        rgba = null;
        problem = null;
        if (!IsJpeg(data))
        {
            problem = "it is not a JPEG file";
            return false;
        }

        try
        {
            var decoder = new Decoder(data);
            rgba = decoder.Decode(out width, out height);
            return true;
        }
        catch (JpegException exception)
        {
            problem = exception.Message;
        }
        catch (Exception exception) when (exception is IndexOutOfRangeException || exception is ArgumentOutOfRangeException)
        {
            problem = "the file is damaged or ends too early";
        }

        width = 0;
        height = 0;
        rgba = null;
        return false;
    }

    private static float[] BuildCosines()
    {
        var table = new float[64];
        for (var x = 0; x < 8; x++)
        {
            for (var u = 0; u < 8; u++)
            {
                var c = u == 0 ? 1.0 / Math.Sqrt(2.0) : 1.0;
                table[x * 8 + u] = (float)(c / 2.0 * Math.Cos((2 * x + 1) * u * Math.PI / 16.0));
            }
        }

        return table;
    }

    private sealed class JpegException : Exception
    {
        public JpegException(string message) : base(message)
        {
        }
    }

    private sealed class Huffman
    {
        private const int LookupBits = 9;

        private readonly int[] _maxCode = new int[18];
        private readonly int[] _valuePointer = new int[17];
        private readonly int[] _minCode = new int[17];
        private readonly byte[] _values;
        private readonly short[] _lookup = new short[1 << LookupBits];

        public Huffman(byte[] counts, byte[] values)
        {
            _values = values;
            for (var index = 0; index < _lookup.Length; index++)
            {
                _lookup[index] = -1;
            }

            var code = 0;
            var pointer = 0;
            for (var length = 1; length <= 16; length++)
            {
                var count = counts[length - 1];
                _valuePointer[length] = pointer;
                _minCode[length] = code;
                if (count == 0)
                {
                    _maxCode[length] = -1;
                }
                else
                {
                    for (var index = 0; index < count; index++)
                    {
                        if (length <= LookupBits)
                        {
                            var shift = LookupBits - length;
                            var first = (code + index) << shift;
                            for (var fill = 0; fill < 1 << shift; fill++)
                            {
                                _lookup[first + fill] = (short)((length << 8) | values[pointer + index]);
                            }
                        }
                    }

                    _maxCode[length] = code + count - 1;
                    pointer += count;
                    code += count;
                }

                code <<= 1;
            }

            _maxCode[17] = int.MaxValue;
        }

        public int Decode(Decoder reader)
        {
            var peek = reader.Peek(LookupBits);
            var entry = _lookup[peek];
            if (entry >= 0)
            {
                reader.Skip(entry >> 8);
                return entry & 0xFF;
            }

            var code = 0;
            for (var length = 1; length <= 16; length++)
            {
                code = (code << 1) | reader.ReadBit();
                if (_maxCode[length] >= 0 && code <= _maxCode[length])
                {
                    return _values[_valuePointer[length] + code - _minCode[length]];
                }
            }

            throw new JpegException("the file has a damaged code");
        }
    }

    private sealed class Component
    {
        public int Id;
        public int H;
        public int V;
        public int QuantTable;
        public int BlocksPerLine;
        public int BlocksPerColumn;
        public int BlocksPerLineForMcu;
        public int BlocksPerColumnForMcu;
        public short[] Blocks;
        public int Pred;
        public Huffman Dc;
        public Huffman Ac;
        public byte[] Plane;
        public int PlaneWidth;
        public int PlaneHeight;
    }

    private sealed class Decoder
    {
        private readonly byte[] _data;
        private readonly ushort[][] _quant = new ushort[4][];
        private readonly Huffman[] _dc = new Huffman[4];
        private readonly Huffman[] _ac = new Huffman[4];
        private readonly List<Component> _components = new();
        private int _pos;
        private uint _bits;
        private int _bitCount;
        private bool _markerHit;
        private int _width;
        private int _height;
        private int _hMax;
        private int _vMax;
        private int _mcusPerLine;
        private int _mcusPerColumn;
        private bool _progressive;
        private bool _frameRead;
        private int _scans;
        private int _restartInterval;
        private int _adobeTransform = -1;
        private bool _jfif;
        private int _orientation = 1;
        private int _eobRun;
        private int _successiveState;
        private int _successiveValue;
        private int _successiveRun;

        public Decoder(byte[] data)
        {
            _data = data;
        }

        public byte[] Decode(out int width, out int height)
        {
            _pos = 2;
            while (true)
            {
                var marker = NextMarker();
                if (marker < 0 || marker == 0xD9)
                {
                    break;
                }

                switch (marker)
                {
                    case 0xC0:
                    case 0xC1:
                    case 0xC2:
                        ReadFrame(marker == 0xC2);
                        break;
                    case 0xC3:
                    case 0xC5:
                    case 0xC6:
                    case 0xC7:
                    case 0xC9:
                    case 0xCA:
                    case 0xCB:
                    case 0xCD:
                    case 0xCE:
                    case 0xCF:
                        throw new JpegException("lossless and arithmetic-coded JPEG files are not read, save it again as a usual JPG or PNG");
                    case 0xC4:
                        ReadHuffmanTables();
                        break;
                    case 0xDB:
                        ReadQuantTables();
                        break;
                    case 0xDD:
                        Length();
                        _restartInterval = ReadUInt16();
                        break;
                    case 0xDA:
                        ReadScan();
                        break;
                    case 0xE0:
                    case 0xE1:
                    case 0xEE:
                        ReadApp(marker);
                        break;
                    default:
                        SkipSegment();
                        break;
                }
            }

            if (_scans == 0)
            {
                throw new JpegException("the file has no image data");
            }

            var rgba = Output();
            return Orient(rgba, out width, out height);
        }

        private int NextMarker()
        {
            while (_pos + 1 < _data.Length)
            {
                if (_data[_pos] != 0xFF)
                {
                    _pos++;
                    continue;
                }

                var marker = _data[_pos + 1];
                if (marker == 0xFF)
                {
                    _pos++;
                    continue;
                }

                _pos += 2;
                if (marker == 0x00 || marker == 0x01 || (marker >= 0xD0 && marker <= 0xD7))
                {
                    continue;
                }

                return marker;
            }

            return -1;
        }

        private int ReadUInt16()
        {
            var value = (_data[_pos] << 8) | _data[_pos + 1];
            _pos += 2;
            return value;
        }

        private int Length()
        {
            var length = ReadUInt16();
            if (length < 2 || _pos + length - 2 > _data.Length)
            {
                throw new JpegException("the file is damaged or ends too early");
            }

            return _pos + length - 2;
        }

        private void SkipSegment() => _pos = Length();

        private void ReadApp(int marker)
        {
            var end = Length();
            var start = _pos;
            if (marker == 0xE0 && Matches(start, end, "JFIF\0"))
            {
                _jfif = true;
            }
            else if (marker == 0xEE && Matches(start, end, "Adobe") && end - start >= 12)
            {
                _adobeTransform = _data[start + 11];
            }
            else if (marker == 0xE1 && Matches(start, end, "Exif\0\0"))
            {
                _orientation = ExifOrientation(start + 6, end);
            }

            _pos = end;
        }

        private bool Matches(int start, int end, string text)
        {
            if (end - start < text.Length)
            {
                return false;
            }

            for (var index = 0; index < text.Length; index++)
            {
                if (_data[start + index] != text[index])
                {
                    return false;
                }
            }

            return true;
        }

        private int ExifOrientation(int tiff, int end)
        {
            if (end - tiff < 8)
            {
                return 1;
            }

            bool little;
            if (_data[tiff] == 'I' && _data[tiff + 1] == 'I')
            {
                little = true;
            }
            else if (_data[tiff] == 'M' && _data[tiff + 1] == 'M')
            {
                little = false;
            }
            else
            {
                return 1;
            }

            int U16(int at) => little ? _data[at] | (_data[at + 1] << 8) : (_data[at] << 8) | _data[at + 1];
            long U32(int at) => little
                ? _data[at] | ((long)_data[at + 1] << 8) | ((long)_data[at + 2] << 16) | ((long)_data[at + 3] << 24)
                : ((long)_data[at] << 24) | ((long)_data[at + 1] << 16) | ((long)_data[at + 2] << 8) | _data[at + 3];

            var offset = U32(tiff + 4);
            if (offset < 8 || tiff + offset + 2 > end)
            {
                return 1;
            }

            var directory = tiff + (int)offset;
            var count = U16(directory);
            for (var index = 0; index < count; index++)
            {
                var entry = directory + 2 + index * 12;
                if (entry + 12 > end)
                {
                    break;
                }

                if (U16(entry) == 0x0112 && U16(entry + 2) == 3)
                {
                    var value = U16(entry + 8);
                    return value >= 1 && value <= 8 ? value : 1;
                }
            }

            return 1;
        }

        private void ReadQuantTables()
        {
            var end = Length();
            while (_pos < end)
            {
                var info = _data[_pos++];
                var precision = info >> 4;
                var id = info & 15;
                if (id > 3)
                {
                    throw new JpegException("the file has a damaged quantization table");
                }

                var table = new ushort[64];
                for (var k = 0; k < 64; k++)
                {
                    table[ZigZag[k]] = (ushort)(precision == 0 ? _data[_pos++] : ReadUInt16());
                }

                _quant[id] = table;
            }

            _pos = end;
        }

        private void ReadHuffmanTables()
        {
            var end = Length();
            while (_pos < end)
            {
                var info = _data[_pos++];
                var id = info & 15;
                if (id > 3)
                {
                    throw new JpegException("the file has a damaged Huffman table");
                }

                var counts = new byte[16];
                var total = 0;
                for (var index = 0; index < 16; index++)
                {
                    counts[index] = _data[_pos++];
                    total += counts[index];
                }

                if (total > 256 || _pos + total > end)
                {
                    throw new JpegException("the file has a damaged Huffman table");
                }

                var values = new byte[total];
                Array.Copy(_data, _pos, values, 0, total);
                _pos += total;
                var table = new Huffman(counts, values);
                if (info >> 4 == 0)
                {
                    _dc[id] = table;
                }
                else
                {
                    _ac[id] = table;
                }
            }

            _pos = end;
        }

        private void ReadFrame(bool progressive)
        {
            if (_frameRead)
            {
                throw new JpegException("the file has more than one image");
            }

            var end = Length();
            var precision = _data[_pos++];
            if (precision != 8)
            {
                throw new JpegException($"{precision}-bit JPEG files are not read, only 8-bit ones");
            }

            _height = ReadUInt16();
            _width = ReadUInt16();
            if (_width == 0 || _height == 0)
            {
                throw new JpegException("the image has no size");
            }

            if (_width > MaxSide || _height > MaxSide)
            {
                throw new JpegException($"the image is {_width}x{_height}, at most {MaxSide}x{MaxSide} is read");
            }

            var count = _data[_pos++];
            if (count != 1 && count != 3 && count != 4)
            {
                throw new JpegException($"JPEG files with {count} color channels are not read");
            }

            for (var index = 0; index < count; index++)
            {
                var id = _data[_pos++];
                var sampling = _data[_pos++];
                var quant = _data[_pos++];
                var component = new Component { Id = id, H = sampling >> 4, V = sampling & 15, QuantTable = quant & 3 };
                if (component.H < 1 || component.H > 4 || component.V < 1 || component.V > 4)
                {
                    throw new JpegException("the file has damaged sampling factors");
                }

                _components.Add(component);
                _hMax = Math.Max(_hMax, component.H);
                _vMax = Math.Max(_vMax, component.V);
            }

            _mcusPerLine = (_width + 8 * _hMax - 1) / (8 * _hMax);
            _mcusPerColumn = (_height + 8 * _vMax - 1) / (8 * _vMax);
            foreach (var component in _components)
            {
                var componentWidth = (_width * component.H + _hMax - 1) / _hMax;
                var componentHeight = (_height * component.V + _vMax - 1) / _vMax;
                component.BlocksPerLine = (componentWidth + 7) / 8;
                component.BlocksPerColumn = (componentHeight + 7) / 8;
                component.BlocksPerLineForMcu = _mcusPerLine * component.H;
                component.BlocksPerColumnForMcu = _mcusPerColumn * component.V;
                component.Blocks = new short[component.BlocksPerLineForMcu * component.BlocksPerColumnForMcu * 64];
            }

            _progressive = progressive;
            _frameRead = true;
            _pos = end;
        }

        private void ReadScan()
        {
            if (!_frameRead)
            {
                throw new JpegException("the file has image data before its size");
            }

            var end = Length();
            var count = _data[_pos++];
            var components = new List<Component>(count);
            for (var index = 0; index < count; index++)
            {
                var id = _data[_pos++];
                var tables = _data[_pos++];
                var component = _components.Find(candidate => candidate.Id == id) ?? throw new JpegException("the file has image data of an unknown channel");
                component.Dc = _dc[(tables >> 4) & 3];
                component.Ac = _ac[tables & 3];
                components.Add(component);
            }

            var spectralStart = _data[_pos++];
            var spectralEnd = _data[_pos++];
            var approximation = _data[_pos++];
            _pos = end;
            var high = approximation >> 4;
            var low = approximation & 15;
            if (!_progressive)
            {
                spectralStart = 0;
                spectralEnd = 63;
                high = 0;
                low = 0;
            }

            if (spectralEnd > 63 || spectralStart > spectralEnd)
            {
                throw new JpegException("the file has a damaged scan");
            }

            foreach (var component in components)
            {
                if (spectralStart == 0 && high == 0 && component.Dc == null || spectralEnd > 0 && component.Ac == null && (!_progressive || spectralStart > 0))
                {
                    throw new JpegException("the file misses a Huffman table");
                }
            }

            DecodeScan(components, spectralStart, spectralEnd, high, low);
            _scans++;
        }

        private void DecodeScan(List<Component> components, int start, int end, int high, int low)
        {
            ResetBits();
            foreach (var component in components)
            {
                component.Pred = 0;
            }

            _eobRun = 0;
            _successiveState = 0;
            var single = components.Count == 1;
            var total = single ? components[0].BlocksPerLine * components[0].BlocksPerColumn : _mcusPerLine * _mcusPerColumn;
            var interval = _restartInterval > 0 ? _restartInterval : total;
            var mcu = 0;
            while (mcu < total)
            {
                for (var step = 0; step < interval && mcu < total; step++, mcu++)
                {
                    if (single)
                    {
                        var component = components[0];
                        var row = mcu / component.BlocksPerLine;
                        var column = mcu % component.BlocksPerLine;
                        DecodeBlock(component, (row * component.BlocksPerLineForMcu + column) * 64, start, end, high, low);
                        continue;
                    }

                    var mcuRow = mcu / _mcusPerLine;
                    var mcuColumn = mcu % _mcusPerLine;
                    foreach (var component in components)
                    {
                        for (var v = 0; v < component.V; v++)
                        {
                            for (var h = 0; h < component.H; h++)
                            {
                                var blockRow = mcuRow * component.V + v;
                                var blockColumn = mcuColumn * component.H + h;
                                DecodeBlock(component, (blockRow * component.BlocksPerLineForMcu + blockColumn) * 64, start, end, high, low);
                            }
                        }
                    }
                }

                if (mcu >= total || !NextRestart())
                {
                    break;
                }

                foreach (var component in components)
                {
                    component.Pred = 0;
                }

                _eobRun = 0;
                _successiveState = 0;
            }

            ResetBits();
        }

        private void DecodeBlock(Component component, int offset, int start, int end, int high, int low)
        {
            if (!_progressive)
            {
                DecodeBaseline(component, offset);
            }
            else if (start == 0)
            {
                if (high == 0)
                {
                    DecodeDcFirst(component, offset, low);
                }
                else if (ReadBit() == 1)
                {
                    component.Blocks[offset] |= (short)(1 << low);
                }
            }
            else if (high == 0)
            {
                DecodeAcFirst(component, offset, start, end, low);
            }
            else
            {
                DecodeAcSuccessive(component, offset, start, end, low);
            }
        }

        private void DecodeBaseline(Component component, int offset)
        {
            var blocks = component.Blocks;
            var size = component.Dc.Decode(this);
            component.Pred += size == 0 ? 0 : Extend(ReadBits(size), size);
            blocks[offset] = (short)component.Pred;
            var k = 1;
            while (k < 64)
            {
                var symbol = component.Ac.Decode(this);
                var bits = symbol & 15;
                var run = symbol >> 4;
                if (bits == 0)
                {
                    if (run < 15)
                    {
                        break;
                    }

                    k += 16;
                    continue;
                }

                k += run;
                if (k > 63)
                {
                    break;
                }

                blocks[offset + ZigZag[k]] = (short)Extend(ReadBits(bits), bits);
                k++;
            }
        }

        private void DecodeDcFirst(Component component, int offset, int low)
        {
            var size = component.Dc.Decode(this);
            var difference = size == 0 ? 0 : Extend(ReadBits(size), size) * (1 << low);
            component.Pred += difference;
            component.Blocks[offset] = (short)component.Pred;
        }

        private void DecodeAcFirst(Component component, int offset, int start, int end, int low)
        {
            if (_eobRun > 0)
            {
                _eobRun--;
                return;
            }

            var blocks = component.Blocks;
            var k = start;
            while (k <= end)
            {
                var symbol = component.Ac.Decode(this);
                var bits = symbol & 15;
                var run = symbol >> 4;
                if (bits == 0)
                {
                    if (run < 15)
                    {
                        _eobRun = ReadBits(run) + (1 << run) - 1;
                        break;
                    }

                    k += 16;
                    continue;
                }

                k += run;
                if (k > 63)
                {
                    break;
                }

                blocks[offset + ZigZag[k]] = (short)(Extend(ReadBits(bits), bits) * (1 << low));
                k++;
            }
        }

        private void DecodeAcSuccessive(Component component, int offset, int start, int end, int low)
        {
            var blocks = component.Blocks;
            var k = start;
            while (k <= end)
            {
                var index = offset + ZigZag[k];
                var sign = blocks[index] < 0 ? -1 : 1;
                switch (_successiveState)
                {
                    case 0:
                    {
                        var symbol = component.Ac.Decode(this);
                        var bits = symbol & 15;
                        var run = symbol >> 4;
                        if (bits == 0)
                        {
                            if (run < 15)
                            {
                                _eobRun = ReadBits(run) + (1 << run);
                                _successiveState = 4;
                            }
                            else
                            {
                                _successiveRun = 16;
                                _successiveState = 1;
                            }
                        }
                        else
                        {
                            if (bits != 1)
                            {
                                throw new JpegException("the file has a damaged refinement scan");
                            }

                            _successiveValue = Extend(ReadBits(bits), bits);
                            _successiveRun = run;
                            _successiveState = run != 0 ? 2 : 3;
                        }

                        continue;
                    }
                    case 1:
                    case 2:
                        if (blocks[index] != 0)
                        {
                            blocks[index] += (short)(sign * (ReadBit() << low));
                        }
                        else
                        {
                            _successiveRun--;
                            if (_successiveRun == 0)
                            {
                                _successiveState = _successiveState == 2 ? 3 : 0;
                            }
                        }

                        break;
                    case 3:
                        if (blocks[index] != 0)
                        {
                            blocks[index] += (short)(sign * (ReadBit() << low));
                        }
                        else
                        {
                            blocks[index] = (short)(_successiveValue * (1 << low));
                            _successiveState = 0;
                        }

                        break;
                    case 4:
                        if (blocks[index] != 0)
                        {
                            blocks[index] += (short)(sign * (ReadBit() << low));
                        }

                        break;
                }

                k++;
            }

            if (_successiveState == 4)
            {
                _eobRun--;
                if (_eobRun == 0)
                {
                    _successiveState = 0;
                }
            }
        }

        private static int Extend(int value, int bits) => value < 1 << (bits - 1) ? value - (1 << bits) + 1 : value;

        private void ResetBits()
        {
            _bits = 0;
            _bitCount = 0;
            _markerHit = false;
        }

        private bool NextRestart()
        {
            ResetBits();
            while (_pos + 1 < _data.Length)
            {
                if (_data[_pos] == 0xFF)
                {
                    var next = _data[_pos + 1];
                    if (next >= 0xD0 && next <= 0xD7)
                    {
                        _pos += 2;
                        return true;
                    }

                    if (next != 0x00 && next != 0xFF)
                    {
                        return false;
                    }
                }

                _pos++;
            }

            return false;
        }

        private void Fill()
        {
            while (_bitCount <= 24)
            {
                var value = 0;
                if (!_markerHit && _pos < _data.Length)
                {
                    value = _data[_pos];
                    if (value == 0xFF)
                    {
                        var next = _pos + 1 < _data.Length ? _data[_pos + 1] : 0xD9;
                        if (next == 0x00)
                        {
                            _pos += 2;
                        }
                        else
                        {
                            _markerHit = true;
                            value = 0;
                        }
                    }
                    else
                    {
                        _pos++;
                    }
                }

                _bits |= (uint)value << (24 - _bitCount);
                _bitCount += 8;
            }
        }

        public int Peek(int count)
        {
            if (_bitCount < count)
            {
                Fill();
            }

            return (int)(_bits >> (32 - count));
        }

        public void Skip(int count)
        {
            _bits <<= count;
            _bitCount -= count;
        }

        public int ReadBit()
        {
            if (_bitCount < 1)
            {
                Fill();
            }

            var bit = (int)(_bits >> 31);
            _bits <<= 1;
            _bitCount--;
            return bit;
        }

        private int ReadBits(int count)
        {
            if (count == 0)
            {
                return 0;
            }

            if (_bitCount < count)
            {
                Fill();
            }

            var value = (int)(_bits >> (32 - count));
            _bits <<= count;
            _bitCount -= count;
            return value;
        }

        private byte[] Output()
        {
            foreach (var component in _components)
            {
                BuildPlane(component);
            }

            var rgba = new byte[_width * _height * 4];
            var count = _components.Count;
            if (count == 1)
            {
                var plane = Upsampled(_components[0]);
                for (int pixel = 0, target = 0; pixel < plane.Length; pixel++, target += 4)
                {
                    rgba[target] = rgba[target + 1] = rgba[target + 2] = plane[pixel];
                    rgba[target + 3] = 255;
                }

                return rgba;
            }

            var first = Upsampled(_components[0]);
            var second = Upsampled(_components[1]);
            var third = Upsampled(_components[2]);
            var fourth = count == 4 ? Upsampled(_components[3]) : null;
            var transform = count == 3 ? IsYCbCr() : _adobeTransform == 2;
            for (int pixel = 0, target = 0; pixel < first.Length; pixel++, target += 4)
            {
                int r = first[pixel], g = second[pixel], b = third[pixel];
                if (transform)
                {
                    var y = (float)first[pixel];
                    var cb = second[pixel] - 128f;
                    var cr = third[pixel] - 128f;
                    r = Clamp(y + 1.402f * cr);
                    g = Clamp(y - 0.344136f * cb - 0.714136f * cr);
                    b = Clamp(y + 1.772f * cb);
                }

                if (fourth != null)
                {
                    if (transform)
                    {
                        r = 255 - r;
                        g = 255 - g;
                        b = 255 - b;
                    }

                    int k = fourth[pixel];
                    if (_adobeTransform < 0)
                    {
                        r = 255 - r;
                        g = 255 - g;
                        b = 255 - b;
                        k = 255 - k;
                    }

                    r = r * k / 255;
                    g = g * k / 255;
                    b = b * k / 255;
                }

                rgba[target] = (byte)r;
                rgba[target + 1] = (byte)g;
                rgba[target + 2] = (byte)b;
                rgba[target + 3] = 255;
            }

            return rgba;
        }

        private bool IsYCbCr()
        {
            if (_adobeTransform >= 0)
            {
                return _adobeTransform != 0;
            }

            if (_jfif)
            {
                return true;
            }

            return !(_components[0].Id == 'R' && _components[1].Id == 'G' && _components[2].Id == 'B');
        }

        private static int Clamp(float value) => value <= 0f ? 0 : value >= 255f ? 255 : (int)(value + 0.5f);

        private void BuildPlane(Component component)
        {
            var quant = _quant[component.QuantTable] ?? throw new JpegException("the file misses a quantization table");
            component.PlaneWidth = component.BlocksPerLine * 8;
            component.PlaneHeight = component.BlocksPerColumn * 8;
            component.Plane = new byte[component.PlaneWidth * component.PlaneHeight];
            Span<float> coefficients = stackalloc float[64];
            Span<float> rows = stackalloc float[64];
            for (var blockRow = 0; blockRow < component.BlocksPerColumn; blockRow++)
            {
                for (var blockColumn = 0; blockColumn < component.BlocksPerLine; blockColumn++)
                {
                    var offset = (blockRow * component.BlocksPerLineForMcu + blockColumn) * 64;
                    InverseDct(component.Blocks, offset, quant, coefficients, rows, component.Plane, component.PlaneWidth, blockColumn * 8, blockRow * 8);
                }
            }

            component.Blocks = null;
        }

        private static void InverseDct(short[] blocks, int offset, ushort[] quant, Span<float> coefficients, Span<float> rows, byte[] plane, int stride, int left, int top)
        {
            var onlyDc = true;
            for (var index = 0; index < 64; index++)
            {
                var value = blocks[offset + index];
                coefficients[index] = value * quant[index];
                if (index > 0 && value != 0)
                {
                    onlyDc = false;
                }
            }

            if (onlyDc)
            {
                var flat = (byte)Clamp(coefficients[0] / 8f + 128f);
                for (var y = 0; y < 8; y++)
                {
                    var line = (top + y) * stride + left;
                    for (var x = 0; x < 8; x++)
                    {
                        plane[line + x] = flat;
                    }
                }

                return;
            }

            for (var v = 0; v < 8; v++)
            {
                for (var x = 0; x < 8; x++)
                {
                    var sum = 0f;
                    for (var u = 0; u < 8; u++)
                    {
                        sum += Cosines[x * 8 + u] * coefficients[v * 8 + u];
                    }

                    rows[v * 8 + x] = sum;
                }
            }

            for (var y = 0; y < 8; y++)
            {
                var line = (top + y) * stride + left;
                for (var x = 0; x < 8; x++)
                {
                    var sum = 0f;
                    for (var v = 0; v < 8; v++)
                    {
                        sum += Cosines[y * 8 + v] * rows[v * 8 + x];
                    }

                    plane[line + x] = (byte)Clamp(sum + 128f);
                }
            }
        }

        private byte[] Upsampled(Component component)
        {
            var result = new byte[_width * _height];
            if (component.H == _hMax && component.V == _vMax)
            {
                for (var y = 0; y < _height; y++)
                {
                    Array.Copy(component.Plane, y * component.PlaneWidth, result, y * _width, _width);
                }

                return result;
            }

            var scaleX = (float)component.H / _hMax;
            var scaleY = (float)component.V / _vMax;
            var maxX = (_width * component.H + _hMax - 1) / _hMax - 1;
            var maxY = (_height * component.V + _vMax - 1) / _vMax - 1;
            var x0 = new int[_width];
            var x1 = new int[_width];
            var fx = new float[_width];
            for (var x = 0; x < _width; x++)
            {
                var source = Math.Max(0f, (x + 0.5f) * scaleX - 0.5f);
                var floor = (int)source;
                x0[x] = Math.Min(floor, maxX);
                x1[x] = Math.Min(floor + 1, maxX);
                fx[x] = source - floor;
            }

            var plane = component.Plane;
            var stride = component.PlaneWidth;
            for (var y = 0; y < _height; y++)
            {
                var source = Math.Max(0f, (y + 0.5f) * scaleY - 0.5f);
                var floor = (int)source;
                var top = Math.Min(floor, maxY) * stride;
                var bottom = Math.Min(floor + 1, maxY) * stride;
                var fy = source - floor;
                var line = y * _width;
                for (var x = 0; x < _width; x++)
                {
                    var a = plane[top + x0[x]] + (plane[top + x1[x]] - plane[top + x0[x]]) * fx[x];
                    var b = plane[bottom + x0[x]] + (plane[bottom + x1[x]] - plane[bottom + x0[x]]) * fx[x];
                    result[line + x] = (byte)Clamp(a + (b - a) * fy);
                }
            }

            return result;
        }

        private byte[] Orient(byte[] rgba, out int width, out int height)
        {
            width = _width;
            height = _height;
            if (_orientation == 1)
            {
                return rgba;
            }

            var swap = _orientation >= 5;
            var targetWidth = swap ? _height : _width;
            var targetHeight = swap ? _width : _height;
            var result = new byte[rgba.Length];
            for (var dy = 0; dy < targetHeight; dy++)
            {
                for (var dx = 0; dx < targetWidth; dx++)
                {
                    int sx, sy;
                    switch (_orientation)
                    {
                        case 2: sx = _width - 1 - dx; sy = dy; break;
                        case 3: sx = _width - 1 - dx; sy = _height - 1 - dy; break;
                        case 4: sx = dx; sy = _height - 1 - dy; break;
                        case 5: sx = dy; sy = dx; break;
                        case 6: sx = dy; sy = _height - 1 - dx; break;
                        case 7: sx = _width - 1 - dy; sy = _height - 1 - dx; break;
                        default: sx = _width - 1 - dy; sy = dx; break;
                    }

                    Buffer.BlockCopy(rgba, (sy * _width + sx) * 4, result, (dy * targetWidth + dx) * 4, 4);
                }
            }

            width = targetWidth;
            height = targetHeight;
            return result;
        }
    }
}
