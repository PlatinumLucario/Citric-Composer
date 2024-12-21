using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Kermalis.EndianBinaryIO;
using System.Diagnostics;
using CitraFileLoader;

namespace KoopaLib
{

    //RIFF Wave.
    public class RIFF
    {

        //Path.
        string path = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);

        public string magic; //RIFF.
        public UInt32 chunkSize; //Size of chunks.
        public string identifier; //WAVE.

        public fmtBlock fmt; //FMT
        public dataBlock data; //DATA


        //FMT block data.
        public struct fmtBlock
        {

            public string magic; //fmt .
            public UInt32 chunkSize; //Size of chunk.
            public UInt16 chunkFormat; //1 = PCM.
            public UInt16 numChannels; //1 - Mono, 2 - Stereo, etc.
            public UInt32 sampleRate; //Sample rate.
            public UInt32 byteRate; //== sampleRate * numChannels * bitsPerSample/8
            public UInt16 blockAlign; //==numChannels * bitsPerSample/8
            public UInt16 bitsPerSample; //8=8 bit, 16=16 bit, etc.

            public byte[] restOfData; //Misc. Data that I don't care about.

        }

        //DATA block data.
        public struct dataBlock
        {

            public string magic; //data.
            public UInt32 chunkSize; //==sampleRate * numChannels * bitsPerSample/8
            public byte[] data; //Raw sound data.

        }



        //Load
        public void load(byte[] b)
        {

            //New stream stuff.
            MemoryStream src = new MemoryStream(b);
            EndianBinaryReader br = new EndianBinaryReader(src);

            //Read stuff.
            magic = br.ReadString_Count(4);
            chunkSize = br.ReadUInt32();
            identifier = br.ReadString_Count(4);

            //FMT
            fmt.magic = br.ReadString_Count(4);
            fmt.chunkSize = br.ReadUInt32();
            fmt.chunkFormat = br.ReadUInt16();
            fmt.numChannels = br.ReadUInt16();
            fmt.sampleRate = br.ReadUInt32();
            fmt.byteRate = br.ReadUInt32();
            fmt.blockAlign = br.ReadUInt16();
            fmt.bitsPerSample = br.ReadUInt16();
            br.ReadBytes(fmt.restOfData = new byte[(int)fmt.chunkSize - 16]);

            //DATA
            data.magic = br.ReadString_Count(4);
            data.chunkSize = br.ReadUInt32();
            br.ReadBytes(data.data = new byte[(int)data.chunkSize]);

        }


        //To bytes.
        public byte[] toBytes(bool fix = true)
        {

            if (fix) { fixOffsets(); }
            MemoryStream o = new MemoryStream();
            BinaryWriter bw = new BinaryWriter(o);

            bw.Write(magic);
            bw.Write(chunkSize);
            bw.Write(identifier);

            //FMT.
            bw.Write(fmt.magic);
            bw.Write(fmt.chunkSize);
            bw.Write(fmt.chunkFormat);
            bw.Write(fmt.numChannels);
            bw.Write(fmt.sampleRate);
            bw.Write(fmt.byteRate);
            bw.Write(fmt.blockAlign);
            bw.Write(fmt.bitsPerSample);
            bw.Write(fmt.restOfData);

            //DATA.
            bw.Write(data.magic);
            bw.Write(data.chunkSize);
            bw.Write(data.data);

            return o.ToArray();

        }


        //Fix offsets.
        public void fixOffsets()
        {

            //Data.
            data.chunkSize = (UInt32)data.data.Length;
            data.magic = "data".ToString();

            //FMT.
            fmt.magic = "fmt ".ToString();
            fmt.chunkSize = 16 + (UInt32)fmt.restOfData.Length;

            //Total.
            magic = "RIFF".ToString();
            identifier = "WAVE".ToString();
            chunkSize = fmt.chunkSize + data.chunkSize + 20;

        }


        //To B_wav.
        public b_wav toGameWav()
        {

            Process p = new Process();
            Directory.SetCurrentDirectory(path + "\\Data\\Tools");
            p.StartInfo.FileName = "WavConvCafe.exe";
            p.StartInfo.Arguments = "-o tmp.bfwav tmp.wav";
            p.StartInfo.CreateNoWindow = true;
            p.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;

            File.WriteAllBytes("tmp.wav", this.toBytes());

            p.Start();
            p.WaitForExit();

            b_wav b = new b_wav();
            b.load(File.ReadAllBytes("tmp.bfwav"));

            File.Delete("tmp.bfwav");
            File.Delete("tmp.wav");

            Directory.SetCurrentDirectory(path);

            return b;

        }


        //To B_wav PCM.
        public b_wav toGameWavPCM()
        {

            //Update first.
            fixOffsets();

            //Make new b_wav.
            b_wav b = new b_wav();

            //Take care of data first.
            b.data.samples = new List<byte[]>();
            b.data.pcm16 = new List<ushort[]>();

            if (fmt.bitsPerSample == 8)
            {

                b.info.soundEncoding = 0;

                List<byte>[] soundData = new List<byte>[fmt.numChannels];
                for (int i = 0; i < soundData.Count(); i++)
                {

                    soundData[i] = new List<byte>();

                    MemoryStream src = new MemoryStream(data.data);
                    EndianBinaryReader br = new EndianBinaryReader(src);

                    br.Stream.Position = i;

                    while (br.Stream.Position < data.chunkSize)
                    {

                        soundData[i].Add(br.ReadByte());
                        try { for (int j = 1; j < fmt.numChannels; j++) { br.ReadByte(); } } catch { }

                    }
                }

                //Now convert the corrected data per channel to the samples.
                b.data.samples = new List<byte[]>();
                foreach (List<byte> x in soundData)
                {
                    b.data.samples.Add(x.ToArray());
                }

                b.info.loopEnd = (UInt32)b.data.samples[0].Count();

            }
            else if (fmt.bitsPerSample == 16)
            {

                b.info.soundEncoding = 1;

                List<UInt16>[] soundData = new List<UInt16>[fmt.numChannels];
                for (int i = 0; i < soundData.Count(); i++)
                {
                    soundData[i] = new List<UInt16>();

                    MemoryStream src = new MemoryStream(data.data);
                    EndianBinaryReader br = new EndianBinaryReader(src);

                    br.Stream.Position = i * 2;

                    while (br.Stream.Position < data.chunkSize)
                    {

                        soundData[i].Add(br.ReadUInt16());
                        try { for (int j = 1; j < fmt.numChannels; j++) { br.ReadUInt16(); } } catch { }

                    }
                }

                //Now convert the corrected data per channel to the samples.
                b.data.samples = new List<byte[]>();
                foreach (List<UInt16> x in soundData)
                {
                    b.data.pcm16.Add(x.ToArray());
                }

                b.info.loopEnd = (UInt32)b.data.pcm16[0].Count();

            }

            //Info.
            b.info.channels = new List<b_wav.channelInfo>();
            for (int i = 0; i < fmt.numChannels; i++)
            {
                b_wav.channelInfo x = new b_wav.channelInfo();
                b.info.channels.Add(x);
            }

            b.info.loop = 0;
            b.info.loopStart = 0;
            b.info.samplingRate = fmt.sampleRate;

            //Return the new b_wav.
            return b;

        }

    }


    //DSP File.
    public class Dsp
    {

        public UInt32 numSamples; //Wave data size divided by 4.
        public UInt32 adpcmNibbles; //ADPCM data size times 2.
        public UInt32 sampleRate; //Sampling rate of the data, expressed in Hertz. Used for WAV/AIFF header generation during decode.

        public UInt16 loopFlag; //Specifies whether the sample is looped. This parameter is stored in big-endian format and is used by the DSP for sample playback.
        public UInt16 format; //Always 0.
        public UInt32 loopStart; //2 for not looped. Is an offset.
        public UInt32 loopEnd; //Is the end offset. (Just use adpcmNibbles - 1)
        public UInt32 always2; //Always 2 because Nintendo logic.
        public Int16[] coefficients; //16 coefficients.

        public UInt16 gain; //Always 0.
        public UInt16 predictor; //Predictor scale.
        public UInt16 yn1; //Yn1.
        public UInt16 yn2; //Yn1.

        public UInt16 loopPredictor; //Loop Predictor.
        public UInt16 loopYn1; //Yn1.
        public UInt16 loopYn2; //Yn1.

        public UInt16 channelCount; //Channel count. 0
        public UInt16 blockFrameCount; //Block frame count. 0

        public UInt16[] padding; //9 Paddings.

        public byte[] data; //Remaining ADPCM data.


        /// <summary>
        /// Load a dsp file.
        /// </summary>
        /// <param name="b"></param>
        public void load(byte[] b)
        {

            MemoryStream src = new MemoryStream(b);
            EndianBinaryReader br = new EndianBinaryReader(src);
            br.Endianness = Endianness.BigEndian;

            numSamples = br.ReadUInt32();
            adpcmNibbles = br.ReadUInt32();
            sampleRate = br.ReadUInt32();

            loopFlag = br.ReadUInt16();
            format = br.ReadUInt16();
            loopStart = br.ReadUInt32();
            loopEnd = br.ReadUInt32();
            always2 = br.ReadUInt32();
            br.ReadInt16s(coefficients = new short[16]);

            gain = br.ReadUInt16();
            predictor = br.ReadUInt16();
            yn1 = br.ReadUInt16();
            yn2 = br.ReadUInt16();

            loopPredictor = br.ReadUInt16();
            loopYn1 = br.ReadUInt16();
            loopYn2 = br.ReadUInt16();

            channelCount = br.ReadUInt16();
            blockFrameCount = br.ReadUInt16();

            br.ReadUInt16s(padding = new ushort[9]);

            br.ReadBytes(data = new byte[(int)adpcmNibbles / 2]);

        }


        /// <summary>
        /// Write a dsp file.
        /// </summary>
        /// <param name="b"></param>
        public byte[] toBytes()
        {

            MemoryStream o = new MemoryStream();
            EndianBinaryWriter bw = new EndianBinaryWriter(o);
            bw.Endianness = Endianness.BigEndian;

            bw.WriteUInt32(numSamples);
            bw.WriteUInt32(adpcmNibbles);
            bw.WriteUInt32(sampleRate);

            bw.WriteUInt16(loopFlag);
            bw.WriteUInt16(format);
            bw.WriteUInt32(loopStart);
            bw.WriteUInt32(loopEnd);
            bw.WriteUInt32(always2);
            bw.WriteInt16s(coefficients);

            bw.WriteUInt16(gain);
            bw.WriteUInt16(predictor);
            bw.WriteUInt16(yn1);
            bw.WriteUInt16(yn2);

            bw.WriteUInt16(loopPredictor);
            bw.WriteUInt16(loopYn1);
            bw.WriteUInt16(loopYn2);

            bw.WriteUInt16(channelCount);
            bw.WriteUInt16(blockFrameCount);

            bw.WriteUInt16s(padding);

            bw.WriteBytes(data);

            return o.ToArray();

        }

    }



    /// <summary>
    /// Citric Isabelle Sound Project.
    /// </summary>
    public class CISP {

        public string magic; //CISP.

        public streamInfo stream; //Stream Data.
        public List<trackInfo> tracks; //Tracks.

        public UInt32 seekSize; //Seek Size.
        public byte[] seekBlock; //Seek block.

        public string channelMagic; //CHAN.
        public List<UInt16[]> channelData; //Channel data.



        /// <summary>
        /// Stream Info.
        /// </summary>
        public struct streamInfo {

            public string magic; //STRM.
            public byte loop; //I'm no expert, but this probably signifies a loop.
            public byte numberOfChannels; //How many channels.
            public UInt32 sampleRate; //Rate of sampling.
            public UInt32 loopStart; //Loop start.
            public UInt32 loopEnd; //Loop end. If nonexistant, number of frames.
            public UInt32 numberOfTracks; //Number of tracks.
            public UInt32 sampleSize; //Size of a channel in bytes.

        }



        /// <summary>
        /// Track Info.
        /// </summary>
        public struct trackInfo {

            public string magic; //TRAC.
            public byte volume; //Volume.
            public byte pan; //Pan.
            public UInt16 flags; //Front Bypass???

            public UInt32 channelCount; //Channel Count.
            public List<byte> channels; //Channels.

        }


        /// <summary>
        /// Read a file.
        /// </summary>
        /// <param name="b"></param>
        public void load(byte[] b) {

            MemoryStream src = new MemoryStream(b);
            EndianBinaryReader br = new EndianBinaryReader(src);
            br.Endianness = Endianness.LittleEndian;

            //Magic.
            magic = br.ReadString_Count(4);

            //Stream.
            stream.magic = br.ReadString_Count(4);
            stream.loop = br.ReadByte();
            stream.numberOfChannels = br.ReadByte();
            stream.sampleRate = br.ReadUInt32();
            stream.loopStart = br.ReadUInt32();
            stream.loopEnd = br.ReadUInt32();
            stream.numberOfTracks = br.ReadUInt32();
            stream.sampleSize = br.ReadUInt32();

            //Tracks.
            tracks = new List<trackInfo>();
            for (int i = 0; i < (int)stream.numberOfTracks; i++)
            {

                trackInfo t = new trackInfo();
                t.magic = br.ReadString_Count(4);
                t.volume = br.ReadByte();
                t.pan = br.ReadByte();
                t.flags = br.ReadUInt16();
                t.channelCount = br.ReadUInt32();
                t.channels = new List<byte>();
                for (int j = 0; j < t.channelCount; j++)
                {
                    t.channels.Add(br.ReadByte());
                }
                tracks.Add(t);

            }

            //Seek.
            seekSize = br.ReadUInt32();
            br.ReadBytes(seekBlock = new byte[(int)seekSize]);

            //Channels.
            channelMagic = br.ReadString_Count(4);
            channelData = new List<UInt16[]>();
            for (int i = 0; i < stream.numberOfChannels; i++)
            {
                List<UInt16> channel = new List<UInt16>();
                for (int j = 0; j < (int)(stream.sampleSize / 2); j++) {
                    channel.Add(br.ReadUInt16());
                }
                channelData.Add(channel.ToArray());
            }

        }


        /// <summary>
        /// Convert file to bytes.
        /// </summary>
        /// <returns></returns>
        public byte[] toBytes() {

            update();

            MemoryStream o = new MemoryStream();
            EndianBinaryWriter bw = new EndianBinaryWriter(o);
            bw.Endianness = Endianness.LittleEndian;

            //Magic.
            bw.WriteChars(magic);

            //Stream.
            bw.WriteChars(stream.magic);
            bw.WriteByte(stream.loop);
            bw.WriteByte(stream.numberOfChannels);
            bw.WriteUInt32(stream.sampleRate);
            bw.WriteUInt32(stream.loopStart);
            bw.WriteUInt32(stream.loopEnd);
            bw.WriteUInt32(stream.numberOfTracks);
            bw.WriteUInt32(stream.sampleSize);

            //Tracks.
            foreach (trackInfo t in tracks) {

                bw.WriteChars(t.magic);
                bw.WriteByte(t.volume);
                bw.WriteByte(t.pan);
                bw.WriteUInt16(t.flags);
                bw.WriteUInt32(t.channelCount);
                foreach (byte c in t.channels) {
                    bw.WriteByte(c);
                }

            }

            //Seek.
            bw.WriteUInt32(seekSize);
            bw.WriteBytes(seekBlock);

            //Channels.
            bw.WriteChars(channelMagic);
            foreach (UInt16[] a in channelData) {
                foreach (UInt16 c in a) {
                bw.WriteUInt16(c);}
            }

            //Return final file.
            return o.ToArray();

        }


        /// <summary>
        /// Update the file.
        /// </summary>
        public void update() {

            magic = "CISP".ToString();
            stream.magic = "STRM".ToString();
            stream.numberOfChannels = (byte)channelData.Count;
            stream.numberOfTracks = (UInt32)tracks.Count;
            if (stream.numberOfChannels == 0)
            {
                stream.sampleSize = 0;
            }
            else {
                stream.sampleSize = (UInt32)(channelData[0].Count() * 2);
            }
            seekBlock = new byte[0];
            seekSize = (UInt32)seekBlock.Length;
            channelMagic = "CHAN".ToString();
            for (int i = 0; i < tracks.Count; i++) {
                trackInfo t = tracks[i];
                t.channelCount = (UInt32)tracks[i].channels.Count;
                t.magic = "TRAC".ToString();
                tracks[i] = t;
            }

        }



        /// <summary>
        /// Create a RIFF.
        /// </summary>
        /// <returns></returns>
        public RIFF toRIFF() {
            this.update();

            RIFF r = new RIFF();
            r.fmt.chunkFormat = 1;
            r.fmt.numChannels = (UInt16)stream.numberOfChannels;
            r.fmt.sampleRate = stream.sampleRate;
            r.fmt.bitsPerSample = 16;
            r.fmt.byteRate = r.fmt.sampleRate * r.fmt.numChannels * r.fmt.bitsPerSample / 8;
            r.fmt.blockAlign = (UInt16)(r.fmt.numChannels * r.fmt.bitsPerSample / 8);
            r.fmt.restOfData = new byte[0];

            MemoryStream o = new MemoryStream();
            EndianBinaryWriter bw = new EndianBinaryWriter(o);
            for (int i = 0; i < channelData[0].Length; i++) {

                for (int j = 0; j < stream.numberOfChannels; j++) {
                    bw.WriteUInt16(channelData[j][i]);
                }

            }

            r.data.data = o.ToArray();

            return r;

        }


        /// <summary>
        /// Convert to Game Wav in a very cheap way.
        /// </summary>
        /// <returns></returns>
        public b_wav toB_wav() {

            this.update();
            b_wav b = new b_wav();
            b = this.toRIFF().toGameWav();

            b.info.loop = stream.loop;
            b.info.loopStart = stream.loopStart;
            b.info.loopEnd = stream.loopEnd;

            return b;

        }



        /// <summary>
        /// Convert to stream.
        /// </summary>
        /// <returns></returns>
        public b_stm toB_stm() {

            this.update();
            b_stm b = new b_stm();
            b = this.toB_wav().toB_stm();
            b.info.stream.loop = stream.loop;
            b.info.stream.loopStart = stream.loopStart;
            b.info.stream.loopEnd = stream.loopEnd;
            b.info.track = new List<b_stm.infoBlock.trackInfo>();
            for (int i = 0; i < tracks.Count; i++) {
                b_stm.infoBlock.trackInfo t = new b_stm.infoBlock.trackInfo();
                t.volume = tracks[i].volume;
                t.pan = tracks[i].pan;
                t.flags = tracks[i].flags;
                t.byteTable = new b_stm.infoBlock.trackInfo.byteTableTrack();
                t.byteTable.count = tracks[i].channelCount;
                t.byteTable.channelIndexes = tracks[i].channels;
                b.info.track.Add(t);
            }
            b.numSamples = channelData[0].Length;
            b.update(endianNess.big, true);

            return b;

        }

    }



    /// <summary>
    /// Binary Beat Identification.
    /// </summary>
    public class Bbid {

        public byte tempo; //Speed to dance at?
        public byte padding; //Always 0.
        public byte count; //Number of entries.
        public byte unknown; //Always 4.

        public List<UInt32> sampleNumbers; //Always 64 entries, unused are 0. Sample numbers to dance at. The amount is doubled, first set is before loop, second set is the loopEnd + loopStart + the corresponding sample id entry.
        public List<byte> danceMoves; //Always 64 entries, unused are 0. Correspond with sample numbers. 1 = Swing Left; 2 = Cheer Right; 3 = Swing Right; 4 = Cheer Right Fast.
        public UInt32[] actionNumbers; //Always 64 entries, and are always 0x4C484440. May be some sort of sequence command?


        /// <summary>
        /// Load a file from bytes.
        /// </summary>
        public void load(byte[] b) {

            MemoryStream src = new MemoryStream(b);
            EndianBinaryReader br = new EndianBinaryReader(src);
            br.Endianness = Endianness.LittleEndian;
            tempo = br.ReadByte();
            padding = br.ReadByte();
            count = br.ReadByte();
            unknown = br.ReadByte();
            sampleNumbers = new List<uint>((int)count).ToList();
            br.ReadUInt32s(sampleNumbers.ToArray());
            var sample1 = new uint[64 - (int)count];
            br.ReadUInt32s(sample1);
            danceMoves = new List<byte>((int)count).ToList();
            br.ReadBytes(danceMoves.ToArray());
            var sample2 = new byte[64 - (int)count];
            br.ReadBytes(sample2);
            br.ReadUInt32s(actionNumbers = new uint[64]);

        }


        /// <summary>
        /// Convert to bytes.
        /// </summary>
        public byte[] toBytes() {

            MemoryStream o = new MemoryStream();
            EndianBinaryWriter bw = new EndianBinaryWriter(o);
            bw.Endianness = Endianness.LittleEndian;

            bw.WriteByte(tempo);
            bw.WriteByte((byte)0);
            bw.WriteByte((byte)sampleNumbers.Count());
            bw.WriteByte(unknown);
            bw.WriteUInt32s(sampleNumbers.ToArray());
            for (int i = 0; i < 64 - sampleNumbers.Count; i++) {
                bw.WriteUInt32((UInt32)0);
            }
            bw.WriteBytes(danceMoves.ToArray());
            for (int i = 0; i < 64 - sampleNumbers.Count; i++)
            {
                bw.WriteByte((byte)0);
            }
            for (int i = 0; i < 64; i++) {
                bw.WriteUInt32((UInt32)0x4c484440);
            }

            return o.ToArray();

        }

    }

}
