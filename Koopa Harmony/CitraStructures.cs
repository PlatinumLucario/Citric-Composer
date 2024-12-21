using System;
using System.IO;
using System.Collections.Generic;
using Kermalis.EndianBinaryIO;
using System.Diagnostics;
using KoopaLib;

/// <summary>
/// Citra structures.
/// </summary>
namespace CitraFileLoader
{
    public class CitraStructures
    {
        public CitraStructures()
        {
        }
    }

    //Endianess
    public enum endianNess {little, big};
        
    /// <summary>
    /// BCSAR or BFSAR. Format: 3DS; WiiU
    /// </summary>
    public class b_sar {

        //Endian.
        public static endianNess endian = new endianNess();

        //General Data.
        public string magic; //CSAR; FSAR
        public UInt16 byteOrder; //0xFFFE; 0xFEFF
        public UInt16 headerSize; //Header size.
        public UInt32 version; //Padding, Major, Minor, Patch.
        public UInt32 fileSize; //Filesize.
        public UInt16 nBlocks; //Number of blocks.
        public UInt16 reserved; //Reserved.

        //Entries.
        public List<blockPointer> blockPointers; //Block pointers.
        public byte[] padding; //Padding to make 32-bit aligned.

        //Blocks.
        public strgBlock strg; //Strg block.
        public infoBlock info; //Info block.
        public fileBlock file; //File block.
        public List<byte[]> miscBlock; //Other type of block.

        /// <summary>
        /// Block pointer.
        /// </summary>
        public struct blockPointer {

            public UInt16 identifier; //Block to point to.
            public UInt16 reserved; //Reserved.
            public UInt32 offset; //Offset to block.
            public UInt32 size; //Block size.

        }


        /// <summary>
        /// Load a file.
        /// </summary>
        public void load(byte[] b) {

            //Set up stream.
            MemoryStream src = new MemoryStream (b);
            EndianBinaryReader br = new EndianBinaryReader (src);

            //Magic.
            magic = br.ReadString_Count (4);

            //Endianess.
            if (string.Join("", magic) == "FSAR") {
                endian = endianNess.big;
                br.Endianness = Endianness.BigEndian;
            } else if (string.Join("", magic) == "CSAR") {
                endian = endianNess.little;
                br.Endianness = Endianness.LittleEndian;
            } else {
                throw new InvalidDataException ();
            }
                

            byteOrder = br.ReadUInt16 ();
            headerSize = br.ReadUInt16 ();
            version = br.ReadUInt32 ();
            fileSize = br.ReadUInt32 ();
            nBlocks = br.ReadUInt16 ();
            reserved = br.ReadUInt16 ();

            //Get entries.
            blockPointers = new List<blockPointer>();
            for (int i = 0; i < (int)nBlocks; i++) {
            
                blockPointer p = new blockPointer();
                p.identifier = br.ReadUInt16 ();
                p.reserved = br.ReadUInt16 ();
                p.offset = br.ReadUInt32 ();
                p.size = br.ReadUInt32 ();
                blockPointers.Add (p);
            
            }

            //Calculate remaining padding 0s.
            padding = new byte[(int)headerSize - (20 + 12 * (int)nBlocks)];
            br.ReadBytes (padding);

            //Read each block.
            miscBlock = new List<byte[]>();
            foreach (blockPointer p in blockPointers) {

                src.Position = (long)p.offset;
                byte[] n = new byte[(int)p.size];
                br.ReadBytes (n);
                switch (p.identifier) {

                case 0x2000:
                    strg = new strgBlock ();
                    strg.load (n, endian);
                    break;

                case 0x2001:
                    info = new infoBlock ();
                    info.load (n, endian);
                    break;

                case 0x2002:
                    file = new fileBlock ();
                    file.load (n, endian);
                    break;

                default:
                    miscBlock.Add (n);
                    break;

                }

            }
                    

        }


        /// <summary>
        /// Convert to bytes.
        /// </summary>
        public byte[] toBytes(endianNess e) {

            //New stream with writers.
            MemoryStream o = new MemoryStream ();
            EndianBinaryWriter bw = new EndianBinaryWriter (o);

            //Update to correct endian.
            update(e);

            //Endian for writer.
            if (e == endianNess.big) {
                bw.Endianness = Endianness.BigEndian;
            } else {
                bw.Endianness = Endianness.LittleEndian;
            }

            //Basic stuff.
            bw.WriteChars (magic);
            bw.WriteUInt16 (byteOrder);
            bw.WriteUInt16 (headerSize);
            bw.WriteUInt32 (version);
            bw.WriteUInt32 (fileSize);
            bw.WriteUInt16 (nBlocks);
            bw.WriteUInt16 (reserved);

            //Write block pointers.
            foreach (blockPointer p in blockPointers) {

                //Write block info.
                bw.WriteUInt16 (p.identifier);
                bw.WriteUInt16 (p.reserved);
                bw.WriteUInt32 (p.offset);
                bw.WriteUInt32 (p.size);

            }

            //Write padding.
            bw.WriteBytes (padding);

            //Write blocks.
            bw.WriteBytes (strg.toBytes (endian));
            bw.WriteBytes (info.toBytes (endian));
            bw.WriteBytes (file.toBytes (endian));
            foreach (byte[] block in miscBlock) {
                bw.WriteBytes (block);
            }
                

            //Return bytes.
            return o.ToArray();

        }


        /// <summary>
        /// Update the file to a specific endian.
        /// </summary>
        /// <param name="e">E.</param>
        public void update(endianNess e) {
        
            //Magic.
            if (e == endianNess.little) {
                magic = "CSAR".ToString ();
            } else {
                magic = "FSAR".ToString ();
            }

            //Endian
            byteOrder = 0xFEFF;

            //Number of blocks.
            nBlocks = (UInt16)(3 + miscBlock.Count);

            //Reserved.
            reserved = 0;


            //Make table of blocks.
            List<UInt32> relativeOffsets = new List<UInt32>(); //Relative offsets of blocks.
            List<UInt32> blockSizes = new List<UInt32>(); //Sizes per each block.
            UInt32 currentBlockOffsets = 0;

            //STRG.
            relativeOffsets.Add(currentBlockOffsets);
            blockSizes.Add ((UInt32)strg.toBytes (endian).Length);
            currentBlockOffsets += (UInt32)strg.toBytes (endian).Length;

            //INFO.
            relativeOffsets.Add(currentBlockOffsets);
            blockSizes.Add ((UInt32)info.toBytes (endian).Length);
            currentBlockOffsets += (UInt32)info.toBytes (endian).Length;

            //FILE.
            relativeOffsets.Add(currentBlockOffsets);
            blockSizes.Add ((UInt32)file.toBytes (endian).Length);
            currentBlockOffsets += (UInt32)file.toBytes (endian).Length;

            //MISC.
            foreach (byte[] block in miscBlock) {

                relativeOffsets.Add (currentBlockOffsets);
                blockSizes.Add ((UInt32)block.Length);
                currentBlockOffsets += (UInt32)block.Length;

            }

            //Get header size.
            List<byte> newPadding = new List<byte>();
            headerSize = (UInt16)(20 + ((UInt16)nBlocks * 12));
            while (headerSize % 16 != 0) {
                headerSize += 1;
                newPadding.Add (0);
            }
            padding = newPadding.ToArray ();

            //Make the block pointers.
            blockPointers = new List<blockPointer>();

            //STRG.
            blockPointer strgP = new blockPointer();
            strgP.identifier = 0x2000;
            strgP.offset = (UInt32)headerSize + relativeOffsets [0];
            strgP.size = blockSizes [0];
            strgP.reserved = 0;
            blockPointers.Add (strgP);

            //INFO.
            blockPointer infoP = new blockPointer();
            infoP.identifier = 0x2001;
            infoP.offset = (UInt32)headerSize + relativeOffsets [1];
            infoP.size = blockSizes [1];
            infoP.reserved = 0;
            blockPointers.Add (infoP);

            //FILE.
            blockPointer fileP = new blockPointer();
            fileP.identifier = 0x2002;
            fileP.offset = (UInt32)headerSize + relativeOffsets [2];
            fileP.size = blockSizes [2];
            fileP.reserved = 0;
            blockPointers.Add (fileP);

            //MISC.
            for (int i = 0; i < miscBlock.Count; i++) {

                blockPointer miscP = new blockPointer ();
                miscP.identifier = (UInt16)(0x2003 + i);
                miscP.offset = (UInt32)headerSize + relativeOffsets [3+i];
                miscP.size = blockSizes [3 + i];
                blockPointers.Add (miscP);

            }

            //Get filesize.
            fileSize = (UInt32)headerSize;
            foreach (UInt32 size in blockSizes) {
                fileSize += size;
            }
        
        }


        /// <summary>
        /// Extract files to folder.
        /// </summary>
        /// <param name="path"></param>
        public void extract(string path, endianNess e) {

            update(e);

            Directory.CreateDirectory(path);
            File.WriteAllBytes(path + "/strg.bin", strg.toBytes(e));
            File.WriteAllBytes(path + "/info.bin", info.toBytes(e));
            File.WriteAllBytes(path + "/file.bin", file.toBytes(e));
            for (int i = 0; i < miscBlock.Count; i++) {

                File.WriteAllBytes(path + "/misc" + i.ToString("D4") + ".bin", miscBlock[i]);

            }

            //Write version.
            MemoryStream o = new MemoryStream();
            EndianBinaryWriter bw = new EndianBinaryWriter(o);
            bw.WriteUInt32(version);
            File.WriteAllBytes(path + "/version.bin", o.ToArray());

        }


        /// <summary>
        /// Pack something to a sound file.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="e"></param>
        public void pack(string path, endianNess e) {

            string[] files = Directory.GetFiles(path);

            strg = new strgBlock();
            strg.load(File.ReadAllBytes(path + "/strg.bin"), e);

            info = new infoBlock();
            info.load(File.ReadAllBytes(path + "/info.bin"), e);

            file = new fileBlock();
            file.load(File.ReadAllBytes(path + "/file.bin"), e);

            EndianBinaryReader br = new EndianBinaryReader(new MemoryStream(File.ReadAllBytes(path + "/version.bin")));
            if (e == endianNess.big)
            {
                br.Endianness = Endianness.BigEndian;
            }
            else {
                br.Endianness = Endianness.LittleEndian;
            }
            version = br.ReadUInt32();

            //Misc. files.
            miscBlock = new List<byte[]>();
            for (int i = 0; i < files.Length; i++) {

                if (Path.GetFileName(files[i]).StartsWith("misc")) {
                    miscBlock.Add(File.ReadAllBytes(files[i]));
                }

            }

            update(e);

        }


    }




    /// <summary>
    /// String block.
    /// </summary>
    public class strgBlock {

        //Endian.
        public endianNess endian;

        //General stuff.
        public string magic; //STRG.
        public UInt32 fileSize; //File size.

        public UInt16 stringTableIdentifier; //0x2400.
        public UInt16 reservedTable; //Reserved space.
        public UInt32 stringTableOffset; //Add 8 to get offset.

        public UInt16 lookupTableIdentifier; //0x2401.
        public UInt16 reservedLookup; //Reserved space.
        public UInt32 lookupTableOffset; //Add 8 to get offset.

        public stringTableRecords tableRecord; //Table record.
        public List<stringEntry> stringEntries; //String entries.

        public lookupTableRecords lookupRecord; //Lookup record.


        /// <summary>
        /// String Table Records.
        /// </summary>
        public struct stringTableRecords {

            public UInt32 nCount; //Amount of records.
            public List<stringTableRecord> records; //Records.

        }


        /// <summary>
        /// Record in a string table.
        /// </summary>
        public struct stringTableRecord {

            public UInt32 identifier; //0x1F01
            public UInt32 offset; //Relative to start of table.
            public UInt32 length; //Subtract by 1 to get the string.

        }


        //String entries.
        public struct stringEntry {

            public string data; //String data.
            public byte seperator; //Seperator.

        }


        /// <summary>
        /// Lookup table records.
        /// </summary>
        public struct lookupTableRecords {

            public UInt32 rootNode; //Root node.
            public UInt32 amountOfNodes; //Amount of records.
            public List<lookupTableRecord> record; //Records
        
        }


        /// <summary>
        /// Lookup table record.
        /// </summary>
        public struct lookupTableRecord {

            public UInt16 leafNodeFlag; //1 if leaf node.
            public UInt16 searchIndex; //Bit index from left.
            public UInt32 leftIndex; //Bit index from left.
            public UInt32 rightIndex; //Bit index from right.
            public UInt32 stringIndex; //Index of string.
            public UInt32 id; //ID of this node.

        }



        /// <summary>
        /// Load a file.
        /// </summary>
        /// <param name="b">The blue component.</param>
        public void load(byte[] b, endianNess endian) {

            //Set endian.
            this.endian = endian;

            //Reader.
            MemoryStream src = new MemoryStream (b);
            EndianBinaryReader br = new EndianBinaryReader (src);

            //Endian.
            if (endian == endianNess.big) {
                br.Endianness = Endianness.BigEndian;
            } else {
                br.Endianness = Endianness.LittleEndian;
            }

            //Stuff.
            magic = br.ReadString_Count(4);
            fileSize = br.ReadUInt32 ();

            stringTableIdentifier = br.ReadUInt16 ();
            reservedTable = br.ReadUInt16 ();
            stringTableOffset = br.ReadUInt32 () + 8;

            lookupTableIdentifier = br.ReadUInt16 ();
            reservedLookup = br.ReadUInt16 ();
            lookupTableOffset = br.ReadUInt32 () + 8;

            //String table.
            br.Stream.Position = (int)stringTableOffset;
            tableRecord.nCount = br.ReadUInt32();
            tableRecord.records = new List<stringTableRecord>();
            for (int i = 0; i < (int)tableRecord.nCount; i++) {

                stringTableRecord s = new stringTableRecord();
                s.identifier = br.ReadUInt32();
                s.offset = br.ReadUInt32();
                s.length = br.ReadUInt32();
                tableRecord.records.Add(s);

            }

            //Read strings.
            stringEntries = new List<stringEntry>();
            for (int i = 0; i < (int)tableRecord.nCount; i++) {

                br.Stream.Position = (int)tableRecord.records[i].offset + 24;
                stringEntry s = new stringEntry();
                s.data = br.ReadString_Count((int)tableRecord.records[i].length - 1);
                s.seperator = br.ReadByte();
                stringEntries.Add(s);

            }

            //Read lookup table.
            br.Stream.Position = (int)lookupTableOffset;
            lookupRecord.rootNode = br.ReadUInt32();
            lookupRecord.amountOfNodes = br.ReadUInt32();
            lookupRecord.record = new List<lookupTableRecord>();
            for (int i = 0; i < (int)lookupRecord.amountOfNodes; i++)
            {

                lookupTableRecord s = new lookupTableRecord();
                s.leafNodeFlag = br.ReadUInt16();
                s.searchIndex = br.ReadUInt16();
                s.leftIndex = br.ReadUInt32();
                s.rightIndex = br.ReadUInt32();
                s.stringIndex = br.ReadUInt32();
                s.id = br.ReadUInt32();
                lookupRecord.record.Add(s);

            }

        }



        public byte[] toBytes(endianNess endian) {
            
            byte[] b = new byte[2];
            return b;

        }

    }


    /// <summary>
    /// Info block.
    /// </summary>
    public class infoBlock {

        byte[] file;


        /// <summary>
        /// Load a file.
        /// </summary>
        /// <param name="b">The blue component.</param>
        public void load(byte[] b, endianNess endian) {

            file = b;

        }


        public byte[] toBytes(endianNess endian) {
            return file;
        }

    }


    /// <summary>
    /// File block.
    /// </summary>
    public class fileBlock {

        byte[] file;


        /// <summary>
        /// Load a file.
        /// </summary>
        /// <param name="b">The blue component.</param>
        public void load(byte[] b, endianNess endian) {

            file = b;

        }


        public byte[] toBytes(endianNess endian) {
            return file;
        }


    }





    /// <summary>
    /// Stream file.
    /// </summary>
    public class b_stm
    {

        string path = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);

        public endianNess endian; //Endian.
        public int numSamples = -1; //Number of samples.

        public string magic; //CSTM, FSTM.
        public UInt16 byteOrder; //0xFEFF Big, 0xFFFE Small.
        public UInt16 headerSize; //Header size.
        public UInt32 version; //File version.
        public UInt32 fileSize; //File size.
        public UInt16 nBlocks; //Always 3.
        public UInt16 padding; //Padding.

        public sizedReference infoRecord; //Info record.
        public sizedReference seekRecord; //Seek record.
        public sizedReference dataRecord; //Data record.

        public byte[] reserved; //Reserved space for alignment.

        public infoBlock info;
        public seekBlock seek;
        public dataBlock data;


        /// <summary>
        /// Info block. 0x20 aligned.
        /// </summary>
        public struct infoBlock
        {

            public string magic; //INFO.
            public UInt32 size; //Size of this block.

            public Reference streamRecord; //Stream record.
            public Reference trackRecord; //Track record. Leads to track references.
            public Reference channelRecord; //Channel record.

            public referenceTable trackReferences; //References to all the tracks.
            public referenceTable channelReferences; //References to all the channels.

            public streamInfo stream; //Stream info.
            public List<trackInfo> track; //Track info.
            public List<channelInfo> channel; //Channel info.



            /// <summary>
            /// Stream info.
            /// </summary>
            public struct streamInfo
            {

                public byte encoding; //0 = PCM8, 1 = PCM16, 2 = DSP ADPCM, 3 = IMA ADPCM.
                public byte loop; //I'm no expert, but this probably signifies a loop.
                public byte numberOfChannels; //How many channels.
                public byte numberOfRegions; //Used to define the amount of regions in Pokemon Red.
                public UInt32 sampleRate; //Rate of sampling.
                public UInt32 loopStart; //Loop start.
                public UInt32 loopEnd; //Loop end. If nonexistant, number of frames.
                public UInt32 sampleBlockCount; //Number of sample blocks.
                public UInt32 sampleBlockSize; //Size of sample block.
                public UInt32 sampleBlockSampleCount; //Amount of samples in the sample block which is shoved with samples in the sample.
                public UInt32 lastSampleBlockSize; //Size of the last sample block, no padding.
                public UInt32 lastSampleBlockSampleCount; //Samples in the last block.
                public UInt32 lastSampleBlockPaddingSize; //Size of padding in last sample block.
                public UInt32 seekSize; //Size of seek data. Seems to be 4 always?
                public UInt32 seekIntervalSampleCount; //Seek interval sample count interval. Always 38?
                public Reference sampleRecord; //Sample record.


                //For V > 2.1? and above.
                public UInt16 regionSize; //Region Size?????
                public UInt16 padding; //Padding.
                public Reference regionRecord; //Region record.

                //For V4 and above.
                public UInt32 originalLoopStart; //Loop start. (AKA 0)
                public UInt32 originalLoopEnd; //Loop End. (AKA number of frames)

            }


            /// <summary>
            /// Track info.
            /// </summary>
            public struct trackInfo
            {

                public byte volume; //Volume.
                public byte pan; //Pan.
                public UInt16 flags; //Front Bypass???
                public Reference byteTableRecord; //Byte table reference.
                public byteTableTrack byteTable; //Byte table.

                public byte[] reserved; //Extra 0s to make structure divisible by 4.

                /// <summary>
                /// Byte table structure.
                /// </summary>
                public struct byteTableTrack
                {

                    public UInt32 count;
                    public List<byte> channelIndexes; //Channel indexes. Index of sample to use for the track.

                }

            }


            /// <summary>
            /// Channel info.
            /// </summary>
            public struct channelInfo
            {

                public Reference sampleRecord; //Sample reference.

                public dspAdpcmInfo dspAdpcm; //Adpcm.
                public imaAdpcmInfo imaAdpcm; //Adpcm.

            }


            /// <summary>
            /// Adpcm info.
            /// </summary>
            public struct dspAdpcmInfo
            {

                public Int16[] coefficients; //16 ADPCM coefficients.
                public UInt16 predScale; //Predictor scale.
                public UInt16 yn1; //History sample 1.
                public UInt16 yn2; //History sample 2.
                public UInt16 loopPredScale; //Loop predictor scale.
                public UInt16 loopYn1; //Loop History sample.
                public UInt16 loopYn2; //Loop History sample 2.
                public UInt16 padding; //Padding.

            }


            /// <summary>
            /// Ima adpcm info.
            /// </summary>
            public struct imaAdpcmInfo
            {

                public UInt16 data; //Data.
                public byte tableIndex; //Table Index.
                public byte padding; //Padding.

                public UInt16 loopData; //Loop data.
                public byte loopTableIndex; //Loop table index.
                public byte loopPadding; //Loop padding.

            }


        }


        /// <summary>
        /// Seek block. 0x20 aligned.
        /// </summary>
        public struct seekBlock
        {

            public string magic; //Magic.
            public UInt32 size; //Blocksize.

            public byte[] data; //Data.

        }



        /// <summary>
        /// Data block. 0x20 aligned.
        /// </summary>
        public struct dataBlock
        {

            public string magic; //DATA.
            public UInt32 fileSize; //File size.
            public byte[] padding; //Extra thicc padding.

            public List<byte[]> samples; //Samples.
            public List<UInt16[]> pcm16; //PCM16.

            public byte[] reserved; //Resered.

        }


        /// <summary>
        /// A reference used to an offset.
        /// </summary>
        public struct Reference
        {

            public UInt16 identifier; //Identifier.
                                      /*
                                          0x0100	Byte Table
                                          0x0101	Reference Table
                                          0x0300	DSP ADPCM Info
                                          0x0301	IMA ADPCM Info
                                          0x1F00	Sample Data
                                          0x4000	Info Block
                                          0x4001	Seek Block
                                          0x4002	Data Block
                                          0x4100	Stream Info
                                          0x4101	Track Info
                                          0x4102	Channel Info
                                          */
            public UInt16 padding; //Padding woo.
            public UInt32 offset; //Offset.

        }


        /// <summary>
        /// Sized reference.
        /// </summary>
        public struct sizedReference
        {

            public Reference r; //Reference.
            public UInt32 size; //Size.

        }


        /// <summary>
        /// Reference table.
        /// </summary>
        public struct referenceTable
        {

            public UInt32 count; //Count of references.
            public List<Reference> references; //References.

        }


        /// <summary>
        /// Load a file.
        /// </summary>
        /// <param name="b"></param>
        public void load(byte[] b)
        {

            MemoryStream src = new MemoryStream(b);
            EndianBinaryReader br = new EndianBinaryReader(src);
            br.Endianness = Endianness.BigEndian;

            //Read magic.
            magic = br.ReadString_Count(4);

            //Get endianess.
            byteOrder = br.ReadUInt16();
            if (byteOrder == 0xFFFE)
            {
                byteOrder = 0xFEFF;
                endian = endianNess.little;
                br.Endianness = Endianness.LittleEndian;
            }
            else
            {
                byteOrder = 0xFFFE;
                endian = endianNess.big;
                br.Endianness = Endianness.BigEndian;
            }

            //Get header length.
            headerSize = br.ReadUInt16();
            version = br.ReadUInt32();
            fileSize = br.ReadUInt32();
            nBlocks = br.ReadUInt16();
            padding = br.ReadUInt16();

            infoRecord = new sizedReference();
            seekRecord = new sizedReference();
            dataRecord = new sizedReference();

            infoRecord.r.identifier = br.ReadUInt16();
            infoRecord.r.padding = br.ReadUInt16();
            infoRecord.r.offset = br.ReadUInt32();
            infoRecord.size = br.ReadUInt32();

            seekRecord.r.identifier = br.ReadUInt16();
            seekRecord.r.padding = br.ReadUInt16();
            seekRecord.r.offset = br.ReadUInt32();
            seekRecord.size = br.ReadUInt32();

            dataRecord.r.identifier = br.ReadUInt16();
            dataRecord.r.padding = br.ReadUInt16();
            dataRecord.r.offset = br.ReadUInt32();
            dataRecord.size = br.ReadUInt32();

            reserved = new byte[(int)(headerSize - br.Stream.Position)];
            br.ReadBytes(reserved);

            if ((int)nBlocks == 2)
            {
                dataRecord = seekRecord;
            }

            //Read INFO.
            br.Stream.Position = (int)infoRecord.r.offset;
            info.magic = br.ReadString_Count(4);
            info.size = br.ReadUInt32();

            info.streamRecord = new Reference();
            info.trackRecord = new Reference();
            info.channelRecord = new Reference();
            info.trackReferences = new referenceTable();
            info.channelReferences = new referenceTable();
            info.track = new List<infoBlock.trackInfo>();
            info.channel = new List<infoBlock.channelInfo>();

            info.streamRecord.identifier = br.ReadUInt16();
            info.streamRecord.padding = br.ReadUInt16();
            info.streamRecord.offset = br.ReadUInt32();

            info.trackRecord.identifier = br.ReadUInt16();
            info.trackRecord.padding = br.ReadUInt16();
            info.trackRecord.offset = br.ReadUInt32();

            info.channelRecord.identifier = br.ReadUInt16();
            info.channelRecord.padding = br.ReadUInt16();
            info.channelRecord.offset = br.ReadUInt32();

            //Stream info.
            br.Stream.Position = (int)(infoRecord.r.offset + 8 + info.streamRecord.offset);
            info.stream.encoding = br.ReadByte();
            info.stream.loop = br.ReadByte();
            info.stream.numberOfChannels = br.ReadByte();
            info.stream.numberOfRegions = br.ReadByte();
            info.stream.sampleRate = br.ReadUInt32();
            info.stream.loopStart = br.ReadUInt32();
            info.stream.loopEnd = br.ReadUInt32();
            info.stream.sampleBlockCount = br.ReadUInt32();
            info.stream.sampleBlockSize = br.ReadUInt32();
            info.stream.sampleBlockSampleCount = br.ReadUInt32();
            info.stream.lastSampleBlockSize = br.ReadUInt32();
            info.stream.lastSampleBlockSampleCount = br.ReadUInt32();
            info.stream.lastSampleBlockPaddingSize = br.ReadUInt32();
            info.stream.seekSize = br.ReadUInt32();
            info.stream.seekIntervalSampleCount = br.ReadUInt32();

            numSamples = (int)(info.stream.sampleBlockCount * info.stream.sampleBlockSampleCount + info.stream.lastSampleBlockSampleCount);

            info.stream.sampleRecord = new Reference();
            info.stream.sampleRecord.identifier = br.ReadUInt16();
            info.stream.sampleRecord.padding = br.ReadUInt16();
            info.stream.sampleRecord.offset = br.ReadUInt32();

            //Track and channel records.
            if (info.trackRecord.offset != 0xFFFFFFFF) //Could be null if no tracks.
            {
                br.Stream.Position = (int)(infoRecord.r.offset + 8 + info.trackRecord.offset);
                info.trackReferences = new referenceTable();
                info.trackReferences.references = new List<Reference>();
                info.trackReferences.count = br.ReadUInt32();
                for (int i = 0; i < (int)info.trackReferences.count; i++)
                {
                    try
                    {
                        Reference r = new Reference();
                        r.identifier = br.ReadUInt16();
                        r.padding = br.ReadUInt16();
                        r.offset = br.ReadUInt32();
                        info.trackReferences.references.Add(r);
                    } catch { }
                }
            }
            br.Stream.Position = (int)(infoRecord.r.offset + 8 + info.channelRecord.offset);
            info.channelReferences = new referenceTable();
            info.channelReferences.references = new List<Reference>();
            info.channelReferences.count = br.ReadUInt32();
            for (int i = 0; i < (int)info.channelReferences.count; i++)
            {
                Reference r = new Reference();
                r.identifier = br.ReadUInt16();
                r.padding = br.ReadUInt16();
                r.offset = br.ReadUInt32();
                info.channelReferences.references.Add(r);
            }

            //Read tracks.
            if (info.trackRecord.offset != 0xFFFFFFFF)
            {

                foreach (Reference r in info.trackReferences.references)
                {
                    try
                    {
                        br.Stream.Position = (UInt32)(infoRecord.r.offset + 8 + info.trackRecord.offset + r.offset);

                        infoBlock.trackInfo t = new infoBlock.trackInfo();
                        t.volume = br.ReadByte();
                        t.pan = br.ReadByte();
                        t.flags = br.ReadUInt16();

                        t.byteTableRecord = new Reference();
                        t.byteTableRecord.identifier = br.ReadUInt16();
                        t.byteTableRecord.padding = br.ReadUInt16();
                        t.byteTableRecord.offset = br.ReadUInt32();

                        br.Stream.Position = (UInt32)(infoRecord.r.offset + 8 + info.trackRecord.offset + r.offset + t.byteTableRecord.offset);
                        t.byteTable = new infoBlock.trackInfo.byteTableTrack();
                        t.byteTable.count = br.ReadUInt32();
                        t.byteTable.channelIndexes = new List<byte>();
                        for (int j = 0; j < t.byteTable.count; j++)
                        {
                            t.byteTable.channelIndexes.Add(br.ReadByte());
                        }

                        int reservedSize = (int)t.byteTable.count;
                        while (reservedSize % 4 != 0)
                        {
                            reservedSize += 1;
                        }
                        t.reserved = new byte[reservedSize - (int)t.byteTable.count];
                        br.ReadBytes(t.reserved);

                        info.track.Add(t);
                    }
                    catch { }
                }
            }

            //Read channels.
            info.channel = new List<infoBlock.channelInfo>();
            foreach (Reference r in info.channelReferences.references)
            {

                br.Stream.Position = (UInt32)(infoRecord.r.offset + 8 + info.channelRecord.offset + r.offset);

                infoBlock.channelInfo c = new infoBlock.channelInfo();
                c.sampleRecord = new Reference();
                c.sampleRecord.identifier = br.ReadUInt16();
                c.sampleRecord.padding = br.ReadUInt16();
                c.sampleRecord.offset = br.ReadUInt32();

                c.dspAdpcm = new infoBlock.dspAdpcmInfo();
                c.imaAdpcm = new infoBlock.imaAdpcmInfo();

                if (info.stream.encoding == 2)
                {

                    br.Stream.Position = (UInt32)(infoRecord.r.offset + 8 + info.channelRecord.offset + r.offset + c.sampleRecord.offset);

                    br.ReadInt16s(c.dspAdpcm.coefficients = new short[16]);
                    c.dspAdpcm.predScale = br.ReadUInt16();
                    c.dspAdpcm.yn1 = br.ReadUInt16();
                    c.dspAdpcm.yn2 = br.ReadUInt16();
                    c.dspAdpcm.loopPredScale = br.ReadUInt16();
                    c.dspAdpcm.loopYn1 = br.ReadUInt16();
                    c.dspAdpcm.loopYn2 = br.ReadUInt16();
                    c.dspAdpcm.padding = br.ReadUInt16();

                }
                else if (info.stream.encoding == 3)
                {

                    br.Stream.Position = (UInt32)(infoRecord.r.offset + 8 + info.channelRecord.offset + r.offset + c.sampleRecord.offset);

                    c.imaAdpcm.data = br.ReadUInt16();
                    c.imaAdpcm.tableIndex = br.ReadByte();
                    c.imaAdpcm.padding = br.ReadByte();

                    c.imaAdpcm.loopData = br.ReadUInt16();
                    c.imaAdpcm.loopTableIndex = br.ReadByte();
                    c.imaAdpcm.loopPadding = br.ReadByte();

                }

                info.channel.Add(c);

            }


            //Read SEEK.
            br.Stream.Position = (int)seekRecord.r.offset;
            seek.magic = br.ReadString_Count(4);
            seek.size = br.ReadUInt32();
            br.ReadBytes(seek.data = new byte[(int)seek.size - 8]);

            //Read DATA.
            br.Stream.Position = (int)dataRecord.r.offset;
            data.magic = br.ReadString_Count(4);
            data.fileSize = br.ReadUInt32();
            br.ReadBytes(data.padding = new byte[(int)info.stream.sampleRecord.offset]);


            //Get padding size.
            int paddingSize = (int)(info.stream.lastSampleBlockPaddingSize - info.stream.lastSampleBlockSize);


            //If Non-PCM16.
            if (info.stream.encoding != 1)
            {

                br.Stream.Position = (int)(dataRecord.r.offset + 8 + data.padding.Length);

                byte[][] rawSampleData = new byte[(int)info.stream.numberOfChannels + 1][];
                br.ReadBytes(rawSampleData[0] = new byte[(int)(info.stream.numberOfChannels * (info.stream.sampleBlockSize * (info.stream.sampleBlockCount - 1)))]);
                for (int i = 1; i < info.stream.numberOfChannels + 1; i++)
                {

                    br.ReadBytes(rawSampleData[i] = new byte[(int)info.stream.lastSampleBlockSize]);
                    br.ReadBytes(new byte[paddingSize]);

                }

                //Combine all the data into a huge byte array.
                MemoryStream o2 = new MemoryStream();
                EndianBinaryWriter bw2 = new EndianBinaryWriter(o2);
                foreach (byte[] by in rawSampleData)
                {
                    bw2.WriteBytes(by);
                }
                byte[] sampleDataCombined = o2.ToArray();
                MemoryStream src2 = new MemoryStream(sampleDataCombined);
                EndianBinaryReader br2 = new EndianBinaryReader(src2);

                //Now time for the hard part: trying to figure out how the f to sort them by channel.
                /*
                List<byte[]>[] sampleData = new List<byte[]>[info.stream.numberOfChannels];
                for (int i = 0; i < info.stream.numberOfChannels; i++) {

                    br2.Position = 0;

                    sampleData[i] = new List<byte[]>();
                    for (int j = 0; j < info.stream.sampleBlockCount - 1; j++) {

                        for (int k = 0; k < i; k++) {
                            br2.ReadBytes((int)info.stream.sampleBlockSize);
                        }

                        sampleData[i].Add(br2.ReadBytes((int)info.stream.sampleBlockSize));

                        for (int l = i; l < info.stream.numberOfChannels-1; l++)
                        {
                            br2.ReadBytes((int)info.stream.sampleBlockSize);
                        }

                    }

                    for (int m = 0; m < i; m++)
                    {
                        br2.ReadBytes((int)info.stream.lastSampleBlockSize);
                    }

                    sampleData[i].Add(br2.ReadBytes((int)info.stream.lastSampleBlockSize));

                }

                //Now convert to a list of samples.
                data.samples = new List<byte[]>();
                foreach (List<byte[]> channel in sampleData) {
                
                    MemoryStream o3 = new MemoryStream ();
                    BinaryWriter bw3 = new BinaryWriter (o3);

                    foreach (byte[] dat in channel) {
                        bw3.Write (dat);
                    }

                    data.samples.Add(o3.ToArray());
                
                }*/

                //Read each block.
                List<byte[]> blocks = new List<byte[]>();
                for (int i = 0; i < (info.stream.sampleBlockCount - 1) * info.stream.numberOfChannels; i++)
                {
                    var block = new byte[(int)info.stream.sampleBlockSize];
                    br2.ReadBytes(block);
                    blocks.Add(block);
                }
                for (int i = 0; i < info.stream.numberOfChannels; i++)
                {
                    var block = new byte[(int)info.stream.lastSampleBlockSize];
                    br2.ReadBytes(block);
                    blocks.Add(block);
                }

                //Convert blocks to samples.
                List<byte[]>[] sampleData = new List<byte[]>[(int)info.stream.numberOfChannels];
                for (int i = 0; i < sampleData.Length; i++)
                {

                    sampleData[i] = new List<byte[]>();

                    for (int j = i; j < blocks.Count; j += info.stream.numberOfChannels)
                    {

                        sampleData[i].Add(blocks[j]);

                    }

                }

                //Now convert to a list of samples.
                data.samples = new List<byte[]>();
                foreach (List<byte[]> channel in sampleData)
                {

                    MemoryStream o3 = new MemoryStream();
                    BinaryWriter bw3 = new BinaryWriter(o3);

                    foreach (byte[] dat in channel)
                    {
                        bw3.Write(dat);
                    }

                    data.samples.Add(o3.ToArray());

                }



            }

            //PCM16. Sorry, I can't fucking do this. PCM16 streams ONLY HAVE 2 BLOCKS, and it fucks with all my code. Sorry for the dissapointment, but other tools exist.
            else
            {

                throw new NotImplementedException();

                /*
                br.Stream.Position = (int)(dataRecord.r.offset + 8 + data.padding.Length);

                UInt16[][] rawSampleData = new UInt16[(int)info.stream.numberOfChannels + 1][];
                rawSampleData[0] = br.ReadUInt16s((int)(info.stream.numberOfChannels * (info.stream.sampleBlockSampleCount * (info.stream.sampleBlockCount - 1))));
                for (int i = 1; i < info.stream.numberOfChannels + 1; i++)
                {

                    rawSampleData[i] = br.ReadUInt16s((int)info.stream.lastSampleBlockSampleCount);
                    br.ReadUInt16s(paddingSize/2);

                }

                //Combine all the data into a huge byte array.
                MemoryStream o2 = new MemoryStream();
                EndianBinaryWriter bw2 = new EndianBinaryWriter(o2);
                bw2.Endianness = bw2.Endianness;
                foreach (UInt16[] by in rawSampleData)
                {
                    bw2.Write(by);
                }
                byte[] sampleDataCombined = o2.ToArray();
                MemoryStream src2 = new MemoryStream(sampleDataCombined);
                EndianBinaryReader br2 = new EndianBinaryReader(src2);
                br2.Endianness = br.Endianness;


                //Read each block.
                List<UInt16[]> blocks = new List<UInt16[]>();
                for (int i = 0; i < (info.stream.sampleBlockCount - 1) * info.stream.numberOfChannels; i++)
                {
                    blocks.Add(br2.ReadUInt16s((int)info.stream.sampleBlockSampleCount));
                }
                for (int i = 0; i < info.stream.numberOfChannels; i++)
                {
                    blocks.Add(br2.ReadUInt16s((int)info.stream.lastSampleBlockSampleCount));
                }

                //Convert blocks to samples.
                List<UInt16[]>[] sampleData = new List<UInt16[]>[(int)info.stream.numberOfChannels];
                for (int i = 0; i < sampleData.Length; i++)
                {

                    sampleData[i] = new List<UInt16[]>();

                    for (int j = i; j < blocks.Count; j += info.stream.numberOfChannels)
                    {

                        sampleData[i].Add(blocks[j]);

                    }

                }

                //Now convert to a list of samples.
                data.pcm16 = new List<UInt16[]>();
                List<byte[]> channelData = new List<byte[]>();
                foreach (List<UInt16[]> channel in sampleData)
                {

                    MemoryStream o3 = new MemoryStream();
                    EndianBinaryWriter bw3 = new EndianBinaryWriter(o3);
                    bw3.Endianness = br.Endianness;

                    foreach (UInt16[] dat in channel)
                    {
                        bw3.Write(dat);
                    }

                    channelData.Add(o3.ToArray());

                }

                //Write read the blocks.
                foreach (byte[] block in channelData) {

                    EndianBinaryReader br4 = new EndianBinaryReader(new MemoryStream(block));
                    br4.Endianness = br.Endianness;

                    List<UInt16> samplers = new List<UInt16>();
                    while (br4.Position != block.Length) {
                        samplers.Add(br4.ReadUInt16());
                    }

                    data.pcm16.Add(samplers.ToArray());

                }
                */
            }


        }





        /// <summary>
        /// Convert to bytes.
        /// </summary>
        /// <param name="b"></param>
        public byte[] toBytes2(endianNess e)
        {

            update(e, false);

            MemoryStream o = new MemoryStream();
            EndianBinaryWriter bw = new EndianBinaryWriter(o);

            //Magic.
            bw.WriteChars(magic);

            //Get endianess.
            if (e == endianNess.little)
            {
                bw.Endianness = Endianness.LittleEndian;
            }
            else
            {
                bw.Endianness = Endianness.BigEndian;
            }
            bw.WriteUInt16(byteOrder);

            //Header stuff.
            bw.WriteUInt16(headerSize);
            bw.WriteUInt32(version);
            bw.WriteUInt32(fileSize);
            bw.WriteUInt16(nBlocks);
            bw.WriteUInt16(padding);

            bw.WriteUInt16(infoRecord.r.identifier);
            bw.WriteUInt16(infoRecord.r.padding);
            bw.WriteUInt32(infoRecord.r.offset);
            bw.WriteUInt32(infoRecord.size);

            bw.WriteUInt16(seekRecord.r.identifier);
            bw.WriteUInt16(seekRecord.r.padding);
            bw.WriteUInt32(seekRecord.r.offset);
            bw.WriteUInt32(seekRecord.size);

            bw.WriteUInt16(dataRecord.r.identifier);
            bw.WriteUInt16(dataRecord.r.padding);
            bw.WriteUInt32(dataRecord.r.offset);
            bw.WriteUInt32(dataRecord.size);

            //Write padding.
            while (bw.Stream.Position % 0x20 != 0)
            {
                bw.WriteByte((byte)0);
            }


            //Write INFO.
            bw.WriteChars(info.magic);
            bw.WriteUInt32(info.size);

            bw.WriteUInt16(info.streamRecord.identifier);
            bw.WriteUInt16(info.streamRecord.padding);
            bw.WriteUInt32(info.streamRecord.offset);

            bw.WriteUInt16(info.trackRecord.identifier);
            bw.WriteUInt16(info.trackRecord.padding);
            bw.WriteUInt32(info.trackRecord.offset);

            bw.WriteUInt16(info.channelRecord.identifier);
            bw.WriteUInt16(info.channelRecord.padding);
            bw.WriteUInt32(info.channelRecord.offset);

            //Stream info.
            bw.WriteByte(info.stream.encoding);
            bw.WriteByte(info.stream.loop);
            bw.WriteByte(info.stream.numberOfChannels);
            bw.WriteByte(info.stream.numberOfRegions);
            bw.WriteUInt32(info.stream.sampleRate);
            bw.WriteUInt32(info.stream.loopStart);
            bw.WriteUInt32(info.stream.loopEnd);
            bw.WriteUInt32(info.stream.sampleBlockCount);
            bw.WriteUInt32(info.stream.sampleBlockSize);
            bw.WriteUInt32(info.stream.sampleBlockSampleCount);
            bw.WriteUInt32(info.stream.lastSampleBlockSize);
            bw.WriteUInt32(info.stream.lastSampleBlockSampleCount);
            bw.WriteUInt32(info.stream.lastSampleBlockPaddingSize);
            bw.WriteUInt32(info.stream.seekSize);
            bw.WriteUInt32(info.stream.seekIntervalSampleCount);

            bw.WriteUInt16(info.stream.sampleRecord.identifier);
            bw.WriteUInt16(info.stream.sampleRecord.padding);
            bw.WriteUInt32(info.stream.sampleRecord.offset);


            //Write track and channel records. I un-nullify all records so I write them.
            bw.WriteUInt32(info.trackReferences.count);
            foreach (Reference r in info.trackReferences.references)
            {
                bw.WriteUInt16(r.identifier);
                bw.WriteUInt16(r.padding);
                bw.WriteUInt32(r.offset);
            }

            bw.WriteUInt32(info.channelReferences.count);
            foreach (Reference r in info.channelReferences.references)
            {
                bw.WriteUInt16(r.identifier);
                bw.WriteUInt16(r.padding);
                bw.WriteUInt32(r.offset);
            }


            //Write tracks.
            foreach (infoBlock.trackInfo t in info.track)
            {

                bw.WriteByte(t.volume);
                bw.WriteByte(t.pan);
                bw.WriteUInt16(t.flags);

                bw.WriteUInt16(t.byteTableRecord.identifier);
                bw.WriteUInt16(t.byteTableRecord.padding);
                bw.WriteUInt32(t.byteTableRecord.offset);

                bw.WriteUInt32(t.byteTable.count);
                for (int j = 0; j < t.byteTable.channelIndexes.Count; j++)
                {
                    bw.WriteByte(t.byteTable.channelIndexes[j]);
                }

                bw.WriteBytes(t.reserved);

            }

            //Write channels.
            foreach (infoBlock.channelInfo c in info.channel)
            {

                bw.WriteUInt16(c.sampleRecord.identifier);
                bw.WriteUInt16(c.sampleRecord.padding);
                bw.WriteUInt32(c.sampleRecord.offset);

            }


            foreach (infoBlock.channelInfo c in info.channel)
            {

                if (info.stream.encoding == 2)
                {

                    bw.WriteInt16s(c.dspAdpcm.coefficients);
                    bw.WriteUInt16(c.dspAdpcm.predScale);
                    bw.WriteUInt16(c.dspAdpcm.yn1);
                    bw.WriteUInt16(c.dspAdpcm.yn2);
                    bw.WriteUInt16(c.dspAdpcm.loopPredScale);
                    bw.WriteUInt16(c.dspAdpcm.loopYn1);
                    bw.WriteUInt16(c.dspAdpcm.loopYn2);
                    bw.WriteUInt16(c.dspAdpcm.padding);

                }
                else if (info.stream.encoding == 3)
                {

                    bw.WriteUInt16(c.imaAdpcm.data);
                    bw.WriteByte(c.imaAdpcm.tableIndex);
                    bw.WriteByte(c.imaAdpcm.padding);

                    bw.WriteUInt16(c.imaAdpcm.loopData);
                    bw.WriteByte(c.imaAdpcm.loopTableIndex);
                    bw.WriteByte(c.imaAdpcm.loopPadding);

                }

            }

            //Write padding.
            while (bw.Stream.Position % 0x20 != 0)
            {
                bw.WriteByte((byte)0);
            }

            //Write SEEK.
            bw.WriteChars(seek.magic);
            bw.WriteUInt32(seek.size);
            bw.WriteBytes(seek.data);


            //Great, time to write the data somehow.
            bw.WriteChars(data.magic);
            bw.WriteUInt32(data.fileSize);
            bw.WriteBytes(data.padding);

            //Write out each channel
            MemoryStream soundDataOut = new MemoryStream();
            EndianBinaryWriter bw2 = new EndianBinaryWriter(soundDataOut);

            int[] positions = new int[(int)info.stream.numberOfChannels];
            for (int j = 0; j < info.stream.sampleBlockCount - 1; j++)
            {

                for (int i = 0; i < info.stream.numberOfChannels; i++)
                {

                    EndianBinaryReader br = new EndianBinaryReader(new MemoryStream(data.samples[i]));
                    br.Stream.Position = positions[i];
                    var sampleBlock = new byte[(int)info.stream.sampleBlockSize];
                    br.ReadBytes(sampleBlock);
                    bw2.WriteBytes(sampleBlock);

                    positions[i] = (int)br.Stream.Position;

                }

            }

            for (int i = 0; i < info.stream.numberOfChannels; i++)
            {

                EndianBinaryReader br = new EndianBinaryReader(new MemoryStream(data.samples[i]));
                br.Stream.Position = positions[i];

                //Write the data then the padding!
                var lastSampleBlock = new byte[(int)info.stream.lastSampleBlockSize];
                br.ReadBytes(lastSampleBlock);
                bw2.WriteBytes(lastSampleBlock);

                int paddingSize = (int)(info.stream.lastSampleBlockPaddingSize - info.stream.lastSampleBlockSize);
                bw2.WriteBytes(new byte[paddingSize]);

            }

            bw.WriteBytes(soundDataOut.ToArray());

            return o.ToArray();
        }


        public byte[] toBytes(endianNess e, RIFF source) {

            update(e, false);

            Directory.SetCurrentDirectory(path + "/Data/Tools/Pack");

            File.WriteAllBytes("tmp.wav", source.toBytes(true));
            Process p = new Process();
            p.StartInfo.CreateNoWindow = true;
            p.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            p.StartInfo.FileName = "BCSTM.bat";
            p.StartInfo.Arguments = "tmp.wav";
            p.Start();
            p.WaitForExit();

            b_stm h = new b_stm();
            data = h.data;
            info.channel = h.info.channel;
            info.stream.encoding = h.info.stream.encoding;
            info.stream.lastSampleBlockSampleCount = h.info.stream.lastSampleBlockSampleCount;
            info.stream.lastSampleBlockSize = h.info.stream.lastSampleBlockSize;
            info.stream.lastSampleBlockPaddingSize = h.info.stream.lastSampleBlockPaddingSize;
            info.stream.loopEnd = h.info.stream.loopEnd;
            info.stream.numberOfChannels = h.info.stream.numberOfChannels;
            info.stream.seekSize = h.info.stream.seekSize;
            info.stream.seekIntervalSampleCount = h.info.stream.seekIntervalSampleCount;
            info.stream.sampleRate = h.info.stream.sampleRate;
            info.stream.sampleBlockSize = h.info.stream.sampleBlockSize;
            info.stream.sampleBlockSampleCount = h.info.stream.sampleBlockSampleCount;
            info.stream.sampleBlockCount = h.info.stream.sampleBlockCount;
            update(e, false);

            Directory.SetCurrentDirectory(path);

            MemoryStream o = new MemoryStream();
            EndianBinaryWriter bw = new EndianBinaryWriter(o);

            //Magic.
            bw.WriteChars(magic);

            //Get endianess.
            if (e == endianNess.little)
            {
                bw.Endianness = Endianness.LittleEndian;
            }
            else
            {
                bw.Endianness = Endianness.BigEndian;
            }
            bw.WriteUInt16(byteOrder);

            //Header stuff.
            bw.WriteUInt16(headerSize);
            bw.WriteUInt32(version);
            bw.WriteUInt32(fileSize);
            bw.WriteUInt16(nBlocks);
            bw.WriteUInt16(padding);

            bw.WriteUInt16(infoRecord.r.identifier);
            bw.WriteUInt16(infoRecord.r.padding);
            bw.WriteUInt32(infoRecord.r.offset);
            bw.WriteUInt32(infoRecord.size);

            bw.WriteUInt16(seekRecord.r.identifier);
            bw.WriteUInt16(seekRecord.r.padding);
            bw.WriteUInt32(seekRecord.r.offset);
            bw.WriteUInt32(seekRecord.size);

            bw.WriteUInt16(dataRecord.r.identifier);
            bw.WriteUInt16(dataRecord.r.padding);
            bw.WriteUInt32(dataRecord.r.offset);
            bw.WriteUInt32(dataRecord.size);

            //Write padding.
            while (bw.Stream.Position % 0x20 != 0)
            {
                bw.WriteByte((byte)0);
            }


            //Write INFO.
            bw.WriteChars(info.magic);
            bw.WriteUInt32(info.size);

            bw.WriteUInt16(info.streamRecord.identifier);
            bw.WriteUInt16(info.streamRecord.padding);
            bw.WriteUInt32(info.streamRecord.offset);

            bw.WriteUInt16(info.trackRecord.identifier);
            bw.WriteUInt16(info.trackRecord.padding);
            bw.WriteUInt32(info.trackRecord.offset);

            bw.WriteUInt16(info.channelRecord.identifier);
            bw.WriteUInt16(info.channelRecord.padding);
            bw.WriteUInt32(info.channelRecord.offset);

            //Stream info.
            bw.WriteByte(info.stream.encoding);
            bw.WriteByte(info.stream.loop);
            bw.WriteByte(info.stream.numberOfChannels);
            bw.WriteByte(info.stream.numberOfRegions);
            bw.WriteUInt32(info.stream.sampleRate);
            bw.WriteUInt32(info.stream.loopStart);
            bw.WriteUInt32(info.stream.loopEnd);
            bw.WriteUInt32(info.stream.sampleBlockCount);
            bw.WriteUInt32(info.stream.sampleBlockSize);
            bw.WriteUInt32(info.stream.sampleBlockSampleCount);
            bw.WriteUInt32(info.stream.lastSampleBlockSize);
            bw.WriteUInt32(info.stream.lastSampleBlockSampleCount);
            bw.WriteUInt32(info.stream.lastSampleBlockPaddingSize);
            bw.WriteUInt32(info.stream.seekSize);
            bw.WriteUInt32(info.stream.seekIntervalSampleCount);

            bw.WriteUInt16(info.stream.sampleRecord.identifier);
            bw.WriteUInt16(info.stream.sampleRecord.padding);
            bw.WriteUInt32(info.stream.sampleRecord.offset);


            //Write track and channel records. I un-nullify all records so I write them.
            bw.WriteUInt32(info.trackReferences.count);
            foreach (Reference r in info.trackReferences.references)
            {
                bw.WriteUInt16(r.identifier);
                bw.WriteUInt16(r.padding);
                bw.WriteUInt32(r.offset);
            }

            bw.WriteUInt32(info.channelReferences.count);
            foreach (Reference r in info.channelReferences.references)
            {
                bw.WriteUInt16(r.identifier);
                bw.WriteUInt16(r.padding);
                bw.WriteUInt32(r.offset);
            }


            //Write tracks.
            foreach (infoBlock.trackInfo t in info.track)
            {

                bw.WriteByte(t.volume);
                bw.WriteByte(t.pan);
                bw.WriteUInt16(t.flags);

                bw.WriteUInt16(t.byteTableRecord.identifier);
                bw.WriteUInt16(t.byteTableRecord.padding);
                bw.WriteUInt32(t.byteTableRecord.offset);

                bw.WriteUInt32(t.byteTable.count);
                for (int j = 0; j < t.byteTable.channelIndexes.Count; j++)
                {
                    bw.WriteByte(t.byteTable.channelIndexes[j]);
                }

                bw.WriteBytes(t.reserved);

            }

            //Write channels.
            foreach (infoBlock.channelInfo c in info.channel)
            {

                bw.WriteUInt16(c.sampleRecord.identifier);
                bw.WriteUInt16(c.sampleRecord.padding);
                bw.WriteUInt32(c.sampleRecord.offset);

            }


            foreach (infoBlock.channelInfo c in info.channel)
            {

                if (info.stream.encoding == 2)
                {

                    bw.WriteInt16s(c.dspAdpcm.coefficients);
                    bw.WriteUInt16(c.dspAdpcm.predScale);
                    bw.WriteUInt16(c.dspAdpcm.yn1);
                    bw.WriteUInt16(c.dspAdpcm.yn2);
                    bw.WriteUInt16(c.dspAdpcm.loopPredScale);
                    bw.WriteUInt16(c.dspAdpcm.loopYn1);
                    bw.WriteUInt16(c.dspAdpcm.loopYn2);
                    bw.WriteUInt16(c.dspAdpcm.padding);

                }
                else if (info.stream.encoding == 3)
                {

                    bw.WriteUInt16(c.imaAdpcm.data);
                    bw.WriteByte(c.imaAdpcm.tableIndex);
                    bw.WriteByte(c.imaAdpcm.padding);

                    bw.WriteUInt16(c.imaAdpcm.loopData);
                    bw.WriteByte(c.imaAdpcm.loopTableIndex);
                    bw.WriteByte(c.imaAdpcm.loopPadding);

                }

            }

            //Write padding.
            while (bw.Stream.Position % 0x20 != 0)
            {
                bw.WriteByte((byte)0);
            }

            //Write SEEK.
            bw.WriteChars(seek.magic);
            bw.WriteUInt32(seek.size);
            bw.WriteBytes(seek.data);


            //Great, time to write the data somehow.
            bw.WriteChars(data.magic);
            bw.WriteUInt32(data.fileSize);
            bw.WriteBytes(data.padding);

            //Write out each channel
            MemoryStream soundDataOut = new MemoryStream();
            EndianBinaryWriter bw2 = new EndianBinaryWriter(soundDataOut);

            int[] positions = new int[(int)info.stream.numberOfChannels];
            for (int j = 0; j < info.stream.sampleBlockCount - 1; j++)
            {

                for (int i = 0; i < info.stream.numberOfChannels; i++)
                {

                    EndianBinaryReader br = new EndianBinaryReader(new MemoryStream(data.samples[i]));
                    br.Stream.Position = positions[i];
                    var sampleBlock = new byte[(int)info.stream.sampleBlockSize];
                    br.ReadBytes(sampleBlock);
                    bw2.WriteBytes(sampleBlock);

                    positions[i] = (int)br.Stream.Position;

                }

            }

            for (int i = 0; i < info.stream.numberOfChannels; i++)
            {

                EndianBinaryReader br = new EndianBinaryReader(new MemoryStream(data.samples[i]));
                br.Stream.Position = positions[i];

                //Write the data then the padding!
                var lastSampleBlock = new byte[(int)info.stream.lastSampleBlockSize];
                br.ReadBytes(lastSampleBlock);
                bw2.WriteBytes(lastSampleBlock);

                int paddingSize = (int)(info.stream.lastSampleBlockPaddingSize - info.stream.lastSampleBlockSize);
                bw2.WriteBytes(new byte[paddingSize]);

            }

            bw.WriteBytes(soundDataOut.ToArray());

            return o.ToArray();

        }


        /// <summary>
        /// Update the final.
        /// </summary>
        /// <param name="e"></param>
        public void update(endianNess e, bool updateLastSampleBlock)
        {

            MemoryStream byteTrackerWhole = new MemoryStream();
            EndianBinaryWriter byteTrackerWholeWriter = new EndianBinaryWriter(byteTrackerWhole);

            //Some basic stuff.
            if (e == endianNess.little)
            {
                magic = "CSTM".ToString();
            }
            else
            {
                magic = "FSTM".ToString();
            }

            byteOrder = 0xFEFF;
            headerSize = 0x40;
            version = 0x02000000;
            fileSize = 0xFFFFFFFF;
            nBlocks = 3;
            padding = 0;

            infoRecord = new sizedReference();
            infoRecord.size = 0xFFFFFFFF;
            infoRecord.r.identifier = 0x4000;
            infoRecord.r.padding = 0;
            infoRecord.r.offset = 0x0040;

            seekRecord = new sizedReference();
            seekRecord.size = seek.size;
            seekRecord.r.identifier = 0x4001;
            seekRecord.r.padding = 0;
            seekRecord.r.offset = 0xFFFFFFFF;

            //dataRecord = new sizedReference();
            //dataRecord.size = 0xFFFFFFFF;
            //dataRecord.r.identifier = 0x4002;
            //dataRecord.r.padding = 0;
            //dataRecord.r.offset = 0xFFFFFFFF;

            reserved = new byte[8];

            byteTrackerWholeWriter.WriteBytes(new byte[0x40]);


            MemoryStream infoT = new MemoryStream();
            EndianBinaryWriter infoTracker = new EndianBinaryWriter(infoT);

            //Info block.
            info.magic = "INFO".ToString();
            info.size = 0xFFFFFFFF;
            info.streamRecord = new Reference();
            info.streamRecord.identifier = 0x4100;
            info.streamRecord.padding = 0;
            info.streamRecord.offset = 0x00000018;

            info.trackRecord = new Reference();
            info.trackRecord.identifier = 0x0101;
            info.trackRecord.padding = 0;
            info.trackRecord.offset = 0x00000050;

            info.channelRecord = new Reference();
            info.channelRecord.identifier = 0x0101;
            info.channelRecord.padding = 0;
            info.channelRecord.offset = 0xFFFFFFFF;

            infoTracker.WriteBytes(new byte[24]);

            info.stream.numberOfRegions = 0;
            info.stream.numberOfChannels = (byte)info.channel.Count;

            //info.stream.seekSize = 4;
            //info.stream.seekIntervalSampleCount = 0x00003800;

            infoTracker.WriteBytes(new byte[56]);

            //Get data.
           // info.stream.sampleBlockSize = 0x2000;
           // info.stream.sampleBlockSampleCount = 14336;
            //info.stream.sampleBlockCount = (UInt32)(Math.Ceiling((decimal)data.samples[0].Length / (decimal)info.stream.sampleBlockSize));

            //if (numSamples < 0) { throw new Exception("No amount of samples specified!"); }
            //else {

                //info.stream.lastSampleBlockSampleCount = (UInt32)(numSamples % info.stream.sampleBlockSampleCount);
                //info.stream.lastSampleBlockSize = (UInt32)Math.Ceiling((decimal)((decimal)(1/.875)*((decimal)info.stream.lastSampleBlockSampleCount/ (decimal)2)));

            //}
            
            //int paddingSize = (int)info.stream.lastSampleBlockSize;
            //while (paddingSize % 0x20 != 0)
            //{
            //    paddingSize += 1;
            //}
            //info.stream.lastSampleBlockPaddingSize = (UInt32)paddingSize;

            //info.stream.sampleRecord = new reference();
            //info.stream.sampleRecord.identifier = 0x1F00;
            //info.stream.sampleRecord.padding = 0;
            //info.stream.sampleRecord.offset = 0x18;




            MemoryStream trackT = new MemoryStream();
            EndianBinaryWriter trackWriter = new EndianBinaryWriter(trackT);

            //Tracks.
            info.trackReferences = new referenceTable();
            info.trackReferences.count = (UInt32)info.track.Count;
            info.trackReferences.references = new List<Reference>();
            if (info.track.Count == 0)
            {
                infoBlock.trackInfo t = new infoBlock.trackInfo();
                t.pan = 64;
                t.volume = 127;
                t.flags = 1;
                t.byteTable = new infoBlock.trackInfo.byteTableTrack();
                t.byteTable.count = info.stream.numberOfChannels;
                t.byteTable.channelIndexes = new List<byte>();
                for (int i = 0; i < info.stream.numberOfChannels; i++)
                {
                    t.byteTable.channelIndexes.Add((byte)i);
                }
                t.byteTableRecord = new Reference();
                t.byteTableRecord.identifier = 0x0100;
                t.byteTableRecord.padding = 0;
                t.byteTableRecord.offset = 0x0C;

                int position = 0x10 + info.stream.numberOfChannels;
                while (position % 4 != 0)
                {
                    position += 1;
                }
                t.reserved = new byte[(int)(position - 0x10 - info.stream.numberOfChannels)];
                info.track.Add(t);

                trackWriter.WriteBytes(new byte[position + info.stream.numberOfChannels]);
                infoTracker.WriteBytes(trackT.ToArray());
                byteTrackerWholeWriter.WriteBytes(trackT.ToArray());

                info.trackReferences = new referenceTable();
                info.trackReferences.count = 1;
                info.trackReferences.references = new List<Reference>();
                info.trackReferences.references.Add(new Reference());

            }
            for (int i = 0; i < info.trackReferences.count; i++)
            {

                Reference r = new Reference();
                r.identifier = 0x4101;
                r.padding = 0;
                r.offset = 0xFFFFFFFF;
                info.trackReferences.references.Add(r);

            }

            for (int i = 0; i < info.track.Count; i++)
            {

                infoBlock.trackInfo t = info.track[i];
                t.byteTableRecord = new Reference();
                t.byteTableRecord.identifier = 0x0100;
                t.byteTableRecord.padding = 0;
                t.byteTableRecord.offset = 0x0C;

                int position = 0x10 + (int)t.byteTable.count;
                while (position % 4 != 0)
                {
                    position += 1;
                }
                t.reserved = new byte[(int)(position - 0x10 - t.byteTable.count)];
                info.track[i] = t;

            }

            info.channelRecord.offset = (UInt32)infoT.ToArray().Length;
            MemoryStream channelT = new MemoryStream();
            EndianBinaryWriter channelWriter = new EndianBinaryWriter(channelT);

            //Channels.
            info.channelReferences = new referenceTable();
            info.channelReferences.count = (UInt32)info.channel.Count;
            info.channelReferences.references = new List<Reference>();
            for (int i = 0; i < info.channel.Count; i++)
            {

                infoBlock.channelInfo c = info.channel[i];

                Reference r = new Reference();
                r.identifier = 0x4102;
                r.padding = 0;
                r.offset = 0xFFFFFFFF;
                info.channelReferences.references.Add(r);

                c.sampleRecord = new Reference();
                switch (info.stream.encoding)
                {
                    case 0:
                    case 1:
                        c.sampleRecord.identifier = 0;
                        c.sampleRecord.padding = 0;
                        c.sampleRecord.offset = 0xFFFFFFFF;
                        break;
                    case 2:
                        c.sampleRecord.identifier = 0x0300;
                        c.sampleRecord.padding = 0;
                        c.sampleRecord.offset = 8;
                        break;
                    case 3:
                        c.sampleRecord.identifier = 0x0301;
                        c.sampleRecord.padding = 0;
                        c.sampleRecord.offset = 8;
                        break;
                }

                info.channel[i] = c;

            }


            seek = new seekBlock();
            seek.magic = "SEEK".ToString();
            seek.data = new byte[(int)((info.stream.sampleBlockCount + 1) * info.stream.seekSize * info.channel.Count)];

            List<byte> extraSeekData = new List<byte>(seek.data);
            while ((extraSeekData.Count+8) % 0x20 != 0) {
                extraSeekData.Add(0);
            }
            seek.data = extraSeekData.ToArray();

            seek.size = (UInt32)(seek.data.Length + 8);
            seekRecord.size = seek.size;

            data.magic = "DATA".ToString();

            /*
            infoTracker.Write(new UInt32[2]);

            info.size = (UInt32)infoT.ToArray().Length;
            byteTrackerWholeWriter.Write(infoT.ToArray());
            */


            //Lazy way to update offsets. :p

            MemoryStream o = new MemoryStream();
            EndianBinaryWriter bw = new EndianBinaryWriter(o);

            //Magic.
            bw.WriteChars(magic);

            //Get endianess.
            if (e == endianNess.little)
            {
                bw.Endianness = Endianness.LittleEndian;
            }
            else
            {
                bw.Endianness = Endianness.BigEndian;
            }
            bw.WriteUInt16(byteOrder);

            //Header stuff.
            bw.WriteUInt16(headerSize);
            bw.WriteUInt32(version);
            bw.WriteUInt32(fileSize);
            bw.WriteUInt16(nBlocks);
            bw.WriteUInt16(padding);

            bw.WriteUInt16(infoRecord.r.identifier);
            bw.WriteUInt16(infoRecord.r.padding);
            bw.WriteUInt32(infoRecord.r.offset);
            bw.WriteUInt32(infoRecord.size);

            bw.WriteUInt16(seekRecord.r.identifier);
            bw.WriteUInt16(seekRecord.r.padding);
            bw.WriteUInt32(seekRecord.r.offset);
            bw.WriteUInt32(seekRecord.size);

            bw.WriteUInt16(dataRecord.r.identifier);
            bw.WriteUInt16(dataRecord.r.padding);
            bw.WriteUInt32(dataRecord.r.offset);
            bw.WriteUInt32(dataRecord.size);

            //Write padding.
            while (bw.Stream.Position % 0x20 != 0)
            {
                bw.WriteByte((byte)0);
            }

            infoRecord.r.offset = (UInt32)o.ToArray().Length;

            //Write INFO.
            bw.WriteChars(info.magic);
            bw.WriteUInt32(info.size);

            int infoStartRecords = o.ToArray().Length;

            bw.WriteUInt16(info.streamRecord.identifier);
            bw.WriteUInt16(info.streamRecord.padding);
            bw.WriteUInt32(info.streamRecord.offset);

            bw.WriteUInt16(info.trackRecord.identifier);
            bw.WriteUInt16(info.trackRecord.padding);
            bw.WriteUInt32(info.trackRecord.offset);

            bw.WriteUInt16(info.channelRecord.identifier);
            bw.WriteUInt16(info.channelRecord.padding);
            bw.WriteUInt32(info.channelRecord.offset);

            info.streamRecord.offset = (UInt32)(o.ToArray().Length - infoStartRecords);

            //Stream info.
            bw.WriteByte(info.stream.encoding);
            bw.WriteByte(info.stream.loop);
            bw.WriteByte(info.stream.numberOfChannels);
            bw.WriteByte(info.stream.numberOfRegions);
            bw.WriteUInt32(info.stream.sampleRate);
            bw.WriteUInt32(info.stream.loopStart);
            bw.WriteUInt32(info.stream.loopEnd);
            bw.WriteUInt32(info.stream.sampleBlockCount);
            bw.WriteUInt32(info.stream.sampleBlockSize);
            bw.WriteUInt32(info.stream.sampleBlockSampleCount);
            bw.WriteUInt32(info.stream.lastSampleBlockSize);
            bw.WriteUInt32(info.stream.lastSampleBlockSampleCount);
            bw.WriteUInt32(info.stream.lastSampleBlockPaddingSize);
            bw.WriteUInt32(info.stream.seekSize);
            bw.WriteUInt32(info.stream.seekIntervalSampleCount);

            bw.WriteUInt16(info.stream.sampleRecord.identifier);
            bw.WriteUInt16(info.stream.sampleRecord.padding);
            bw.WriteUInt32(info.stream.sampleRecord.offset);

            info.trackRecord.offset = (UInt32)(o.ToArray().Length - infoStartRecords);

            //Write track and channel records. I un-nullify all records so I write them.
            int trackOffsetTracker = o.ToArray().Length;
            bw.WriteUInt32(info.trackReferences.count);
            foreach (Reference r in info.trackReferences.references)
            {
                bw.WriteUInt16(r.identifier);
                bw.WriteUInt16(r.padding);
                bw.WriteUInt32(r.offset);
            }

            info.channelRecord.offset = (UInt32)(o.ToArray().Length - infoStartRecords);

            int channelOffsetTracker = o.ToArray().Length;
            bw.WriteUInt32(info.channelReferences.count);
            foreach (Reference r in info.channelReferences.references)
            {
                bw.WriteUInt16(r.identifier);
                bw.WriteUInt16(r.padding);
                bw.WriteUInt32(r.offset);
            }


            //Write tracks.
            int trackCount = 0;
            foreach (infoBlock.trackInfo t in info.track)
            {

                Reference r = info.trackReferences.references[trackCount];
                r.offset = (UInt32)(o.ToArray().Length - trackOffsetTracker);
                info.trackReferences.references[trackCount] = r;
                trackCount += 1;

                bw.WriteByte(t.volume);
                bw.WriteByte(t.pan);
                bw.WriteUInt16(t.flags);

                bw.WriteUInt16(t.byteTableRecord.identifier);
                bw.WriteUInt16(t.byteTableRecord.padding);
                bw.WriteUInt32(t.byteTableRecord.offset);

                bw.WriteUInt32(t.byteTable.count);
                for (int j = 0; j < t.byteTable.channelIndexes.Count; j++)
                {
                    bw.WriteByte(t.byteTable.channelIndexes[j]);
                }

                bw.WriteBytes(t.reserved);

            }


            //Write channels.
            int channelCount = 0;


            MemoryStream o2 = new MemoryStream();
            EndianBinaryWriter bw3 = new EndianBinaryWriter(o2);
            int randCounter = 0;

            for (int i = 0; i < info.channel.Count; i++)
            {
                bw3.WriteUInt16(info.channel[i].sampleRecord.identifier);
                bw3.WriteUInt16(info.channel[i].sampleRecord.padding);
                bw3.WriteUInt32(info.channel[i].sampleRecord.offset);
            }

            for (int i = 0; i < info.channel.Count; i++)
            {

                infoBlock.channelInfo r = info.channel[i];
                r.sampleRecord.offset = (UInt32)(o2.ToArray().Length - randCounter);
                info.channel[i] = r;
                randCounter += 8;

                if (info.stream.encoding == 2)
                {

                    bw3.WriteInt16s(info.channel[i].dspAdpcm.coefficients);
                    bw3.WriteUInt16(info.channel[i].dspAdpcm.predScale);
                    bw3.WriteUInt16(info.channel[i].dspAdpcm.yn1);
                    bw3.WriteUInt16(info.channel[i].dspAdpcm.yn2);
                    bw3.WriteUInt16(info.channel[i].dspAdpcm.loopPredScale);
                    bw3.WriteUInt16(info.channel[i].dspAdpcm.loopYn1);
                    bw3.WriteUInt16(info.channel[i].dspAdpcm.loopYn2);
                    bw3.WriteUInt16(info.channel[i].dspAdpcm.padding);

                }
                else if (info.stream.encoding == 3)
                {

                    bw3.WriteUInt16(info.channel[i].imaAdpcm.data);
                    bw3.WriteByte(info.channel[i].imaAdpcm.tableIndex);
                    bw3.WriteByte(info.channel[i].imaAdpcm.padding);

                    bw3.WriteUInt16(info.channel[i].imaAdpcm.loopData);
                    bw3.WriteByte(info.channel[i].imaAdpcm.loopTableIndex);
                    bw3.WriteByte(info.channel[i].imaAdpcm.loopPadding);

                }

            }



            foreach (infoBlock.channelInfo c in info.channel)
            {

                Reference r = info.channelReferences.references[channelCount];
                r.offset = (UInt32)(o.ToArray().Length - channelOffsetTracker);
                info.channelReferences.references[channelCount] = r;
                channelCount += 1;

                bw.WriteUInt16(c.sampleRecord.identifier);
                bw.WriteUInt16(c.sampleRecord.padding);
                bw.WriteUInt32(c.sampleRecord.offset);

            }

            foreach (infoBlock.channelInfo c in info.channel)
            {

                if (info.stream.encoding == 2)
                {

                    bw.WriteInt16s(c.dspAdpcm.coefficients);
                    bw.WriteUInt16(c.dspAdpcm.predScale);
                    bw.WriteUInt16(c.dspAdpcm.yn1);
                    bw.WriteUInt16(c.dspAdpcm.yn2);
                    bw.WriteUInt16(c.dspAdpcm.loopPredScale);
                    bw.WriteUInt16(c.dspAdpcm.loopYn1);
                    bw.WriteUInt16(c.dspAdpcm.loopYn2);
                    bw.WriteUInt16(c.dspAdpcm.padding);

                }
                else if (info.stream.encoding == 3)
                {

                    bw.WriteUInt16(c.imaAdpcm.data);
                    bw.WriteByte(c.imaAdpcm.tableIndex);
                    bw.WriteByte(c.imaAdpcm.padding);

                    bw.WriteUInt16(c.imaAdpcm.loopData);
                    bw.WriteByte(c.imaAdpcm.loopTableIndex);
                    bw.WriteByte(c.imaAdpcm.loopPadding);

                }

            }

            //Write padding.
            while (bw.Stream.Position % 0x20 != 0)
            {
                bw.WriteByte((byte)0);
            }

            infoRecord.size = (UInt32)(o.ToArray().Length - infoRecord.r.offset);
            info.size = infoRecord.size;
            seekRecord.r.offset = (UInt32)o.ToArray().Length;

            //Write SEEK.
            bw.WriteChars(seek.magic);
            bw.WriteUInt32(seek.size);
            bw.WriteBytes(seek.data);


            seek.size = (UInt32)(o.ToArray().Length - seekRecord.r.offset);
            seekRecord.size = seek.size;
            dataRecord.r.offset = (UInt32)o.ToArray().Length;

            fileSize = (UInt32)info.size + headerSize + data.fileSize;

        }



        /// <summary>
        /// Convert to b_wav.
        /// </summary>
        public b_wav toB_wav()
        {

            update(endianNess.big, false);

            b_wav b = new b_wav();
            b.info.loop = info.stream.loop;
            b.info.loopEnd = info.stream.loopEnd;
            b.info.loopStart = info.stream.loopStart;

            b.info.soundEncoding = info.stream.encoding;
            b.info.samplingRate = info.stream.sampleRate;

            b.info.channels = new List<b_wav.channelInfo>();
            for (int i = 0; i < info.channel.Count; i++)
            {

                b_wav.channelInfo c = new b_wav.channelInfo();

                if (b.info.soundEncoding == 2)
                {

                    c.dspAdpcm.coefficients = info.channel[i].dspAdpcm.coefficients;
                    c.dspAdpcm.loopPredScale = info.channel[i].dspAdpcm.loopPredScale;
                    c.dspAdpcm.loopYn1 = info.channel[i].dspAdpcm.loopYn1;
                    c.dspAdpcm.loopYn2 = info.channel[i].dspAdpcm.loopYn2;
                    c.dspAdpcm.padding = info.channel[i].dspAdpcm.padding;
                    c.dspAdpcm.predScale = info.channel[i].dspAdpcm.predScale;
                    c.dspAdpcm.yn1 = info.channel[i].dspAdpcm.yn1;
                    c.dspAdpcm.yn2 = info.channel[i].dspAdpcm.yn2;

                }
                else if (b.info.soundEncoding == 3)
                {

                    c.imaAdpcm.data = info.channel[i].imaAdpcm.data;
                    c.imaAdpcm.loopData = info.channel[i].imaAdpcm.loopData;
                    c.imaAdpcm.loopPadding = info.channel[i].imaAdpcm.loopPadding;
                    c.imaAdpcm.loopTableIndex = info.channel[i].imaAdpcm.loopTableIndex;
                    c.imaAdpcm.padding = info.channel[i].imaAdpcm.padding;
                    c.imaAdpcm.tableIndex = info.channel[i].imaAdpcm.tableIndex;

                }

                b.info.channels.Add(c);

            }

            b.data.samples = data.samples;
            b.data.pcm16 = data.pcm16;

            return b;

        }



        /// <summary>
        /// Convert to RIFF wave. ONLY SUPPORTS PCM B_WAVs!!!
        /// </summary>
        /// <returns></returns>
        public RIFF toRiff()
        {

            //Make new riff.
            RIFF r = new RIFF();

            //General data.
            r.fmt.chunkFormat = 1;
            r.fmt.numChannels = (UInt16)info.channel.Count;
            r.fmt.sampleRate = info.stream.sampleRate;
            r.fmt.restOfData = new byte[0];


            //Different encoding related data:
            switch (info.stream.encoding)
            {

                //PCM8.
                case 0:

                    //Data block. A wave has each sample alternate, where Nintendo has the first channel then the other.
                    byte[][] sampleData = data.samples.ToArray();
                    MemoryStream waveSampleData = new MemoryStream();
                    BinaryWriter bw = new BinaryWriter(waveSampleData);
                    foreach (byte[] b in sampleData)
                    {
                        bw.Write(b);
                    }
                    r.data.data = waveSampleData.ToArray();

                    //Rest of fmt.
                    r.fmt.bitsPerSample = 8;
                    r.fmt.byteRate = r.fmt.sampleRate * r.fmt.numChannels * r.fmt.bitsPerSample / 8;
                    r.fmt.blockAlign = (UInt16)(r.fmt.numChannels * r.fmt.bitsPerSample / 8);

                    break;

                //DSP-ADPCM Oh boy, here is the big one.
                case 2:

                    //Create the DSP blocks.
                    List<Dsp> dsps = new List<Dsp>();
                    for (int i = 0; i < info.channel.Count; i++)
                    {

                        Dsp d = new Dsp();
                        d.always2 = 2;
                        d.adpcmNibbles = (UInt32)(data.samples[i].Length * info.channel.Count);
                        d.blockFrameCount = 0;
                        d.channelCount = 0;
                        d.coefficients = info.channel[i].dspAdpcm.coefficients;
                        d.data = data.samples[i];
                        d.format = 0;
                        d.gain = 0;
                        d.loopEnd = d.adpcmNibbles - 1;
                        d.loopFlag = 0;
                        d.loopPredictor = info.channel[i].dspAdpcm.loopPredScale;
                        d.loopStart = 2;
                        d.loopYn1 = 0;
                        d.loopYn2 = 0;
                        d.numSamples = (UInt32)(info.stream.sampleBlockSampleCount * (info.stream.sampleBlockCount - 1) + info.stream.lastSampleBlockSampleCount);
                        d.padding = new UInt16[9];
                        d.predictor = info.channel[i].dspAdpcm.predScale;
                        d.sampleRate = info.stream.sampleRate;
                        d.yn1 = info.channel[i].dspAdpcm.yn1;
                        d.yn2 = info.channel[i].dspAdpcm.yn2;

                        dsps.Add(d);

                    }

                    //Convert each DSP, and extract it's juicy new wave file.
                    List<RIFF> riffs = new List<RIFF>();
                    foreach (Dsp d in dsps)
                    {

                        Directory.SetCurrentDirectory(path + "\\Data\\Tools");
                        File.WriteAllBytes("tmp.dsp", d.toBytes());

                        Process p = new Process();
                        p.StartInfo.Arguments = "-D tmp.dsp tmp.wav";
                        p.StartInfo.CreateNoWindow = true;
                        p.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                        p.StartInfo.FileName = "dspadpcm.exe";
                        p.Start();
                        p.WaitForExit();

                        RIFF riff = new RIFF();
                        riff.load(File.ReadAllBytes("tmp.wav"));
                        riffs.Add(riff);

                        File.Delete("tmp.dsp");
                        File.Delete("tmp.wav");

                        /*
                        foreach (string s in Directory.EnumerateFiles(path + "Data\\Tools")) {
                            if (s.EndsWith(".txt")) { File.Delete(s); }
                        }
                        */
                        Directory.SetCurrentDirectory(path);


                    }

                    //Now that we have the RIFF(s), we can now write a SUPER-RIFF with all the channels.
                    r.fmt.bitsPerSample = 16;
                    r.fmt.byteRate = r.fmt.sampleRate * r.fmt.numChannels * r.fmt.bitsPerSample / 8;
                    r.fmt.blockAlign = (UInt16)(r.fmt.numChannels * r.fmt.bitsPerSample / 8);

                    //Here is the hard part, where we write each channel correctly.
                    MemoryStream newSampleData = new MemoryStream();
                    EndianBinaryWriter bw3 = new EndianBinaryWriter(newSampleData);

                    int byteCount = 0;
                    while (bw3.Stream.Position < riffs[0].data.data.Length * info.channel.Count)
                    {

                        foreach (RIFF f in riffs)
                        {
                            try { bw3.WriteByte(f.data.data[byteCount]); } catch { }
                            try { bw3.WriteByte(f.data.data[byteCount + 1]); } catch { }
                        }

                        byteCount += 2;

                    }

                    r.data.data = newSampleData.ToArray();

                    break;

            }

            //Return the final riff.
            return r;

        }



    }







    /// <summary>
    /// Wave file.
    /// </summary>
    public class b_wav
    {

        string path = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);

        public endianNess endian; //Endian.

        public string magic; //CWAV, FWAV.
        public UInt16 byteOrder; //0xFEFF Big, 0xFFEF Small.
        public UInt16 headerSize; //Header Size.
        public UInt32 version; //Version of the format.
        public UInt32 fileSize; //File size.
        public UInt16 nBlocks; //Always 2.
        public UInt16 padding; //Useless padding.

        public sizedReference infoRecord; //Info Record.
        public sizedReference dataRecord; //Data Record.

        public byte[] reserved; //Reserved to make header size divisible by 0x40.

        //Blocks.
        public infoBlock info; //Info.
        public dataBlock data; //Data.

        /// <summary>
        /// Info block. 0x20 aligned.
        /// </summary>
        public struct infoBlock
        {

            public string magic; //INFO.
            public UInt32 size; //Size of the block.
            public byte soundEncoding; //PCM8, PCM16, DSP ADPCM, IMA ADPCM.
            public byte loop; //If there is a loop.
            public UInt16 padding; //Nintendo loves it.
            public UInt32 samplingRate; //Sampling rate.
            public UInt32 loopStart; //Loop start.
            public UInt32 loopEnd; //Loop end.
            public UInt32 padding2; //More padding.

            public referenceTable channelRecords; //References. Leads to channel info(s).
            public List<channelInfo> channels; //Channels.

            public byte[] reserved; //Remaining reserved space.


        }



        /// <summary>
        /// Channel info.
        /// </summary>
        public struct channelInfo
        {

            public Reference sampleRecord; //Sample reference.
            public Reference adpcmRecord; //Adpcm reference.
            public UInt32 reserved; //Reserved.

            public dspAdpcmInfo dspAdpcm; //Adpcm.
            public imaAdpcmInfo imaAdpcm; //Adpcm.

        }


        /// <summary>
        /// Adpcm info.
        /// </summary>
        public struct dspAdpcmInfo
        {

            public Int16[] coefficients; //16 ADPCM coefficients.
            public UInt16 predScale; //Predictor scale.
            public UInt16 yn1; //History sample 1.
            public UInt16 yn2; //History sample 2.
            public UInt16 loopPredScale; //Loop predictor scale.
            public UInt16 loopYn1; //Loop History sample.
            public UInt16 loopYn2; //Loop History sample 2.
            public UInt16 padding; //Padding.

        }


        /// <summary>
        /// Ima adpcm info.
        /// </summary>
        public struct imaAdpcmInfo
        {

            public UInt16 data; //Data.
            public byte tableIndex; //Table Index.
            public byte padding; //Padding.

            public UInt16 loopData; //Loop data.
            public byte loopTableIndex; //Loop table index.
            public byte loopPadding; //Loop padding.

        }


        /// <summary>
        /// Data block. 0x20 aligned.
        /// </summary>
        public struct dataBlock
        {

            public string magic; //DATA.
            public UInt32 size; //Size of this block.

            public byte[] padding; //Padding.
            public List<byte[]> samples; //Samples.

            public List<UInt16[]> pcm16; //PCM16 samples. Only needs to be bothered with with endianess.

        }




        /// <summary>
        /// A reference used to an offset.
        /// </summary>
        public struct Reference
        {

            public UInt16 identifier; //Identifier.
                                      /*
                                          0x0100	Byte Table
                                          0x0101	Reference Table
                                          0x0300	DSP ADPCM Info
                                          0x0301	IMA ADPCM Info
                                          0x1F00	Sample Data
                                          0x4000	Info Block
                                          0x4001	Seek Block
                                          0x4002	Data Block
                                          0x4100	Stream Info
                                          0x4101	Track Info
                                          0x4102	Channel Info
                                          */
            public UInt16 padding; //Padding woo.
            public UInt32 offset; //Offset.

        }


        /// <summary>
        /// Sized reference.
        /// </summary>
        public struct sizedReference
        {

            public Reference r; //Reference.
            public UInt32 size; //Size.

        }


        /// <summary>
        /// Reference table.
        /// </summary>
        public struct referenceTable
        {

            public UInt32 count; //Count of references.
            public List<Reference> references; //References.

        }





        /// <summary>
        /// Load a file.
        /// </summary>
        public void load(byte[] b)
        {

            //Reader stuff.
            MemoryStream src = new MemoryStream(b);
            EndianBinaryReader br = new EndianBinaryReader(src);
            br.Endianness = Endianness.BigEndian;

            //Read magic.
            magic = br.ReadString_Count(4);

            //Get endianess.
            byteOrder = br.ReadUInt16();

            if (byteOrder == 0xFEFF)
            {

                br.Endianness = Endianness.BigEndian;
                endian = endianNess.big;

            }
            else if (byteOrder == 0xFFFE)
            {

                br.Endianness = Endianness.LittleEndian;
                endian = endianNess.little;
                byteOrder = 0xFEFF;

            }

            //Other data.
            headerSize = br.ReadUInt16();
            version = br.ReadUInt32();
            fileSize = br.ReadUInt32();
            nBlocks = br.ReadUInt16();
            padding = br.ReadUInt16();

            //Main references.
            infoRecord = new sizedReference();
            infoRecord.r = new Reference();
            infoRecord.r.identifier = br.ReadUInt16();
            infoRecord.r.padding = br.ReadUInt16();
            infoRecord.r.offset = br.ReadUInt32();
            infoRecord.size = br.ReadUInt32();

            dataRecord = new sizedReference();
            dataRecord.r = new Reference();
            dataRecord.r.identifier = br.ReadUInt16();
            dataRecord.r.padding = br.ReadUInt16();
            dataRecord.r.offset = br.ReadUInt32();
            dataRecord.size = br.ReadUInt32();


            //Info block.
            info = new infoBlock();
            br.Stream.Position = (int)infoRecord.r.offset;

            info.magic = br.ReadString_Count(4);
            info.size = br.ReadUInt32();
            info.soundEncoding = br.ReadByte();
            info.loop = br.ReadByte();
            info.padding = br.ReadUInt16();
            info.samplingRate = br.ReadUInt32();
            info.loopStart = br.ReadUInt32();
            info.loopEnd = br.ReadUInt32();
            info.padding2 = br.ReadUInt32();

            //Read channel record.
            long channelRecordStart = br.Stream.Position;
            info.channelRecords = new referenceTable();
            info.channelRecords.count = br.ReadUInt32();
            info.channelRecords.references = new List<Reference>();
            for (int i = 0; i < (int)info.channelRecords.count; i++)
            {

                Reference r = new Reference();
                r.identifier = br.ReadUInt16();
                r.padding = br.ReadUInt16();
                r.offset = br.ReadUInt32();
                info.channelRecords.references.Add(r);

            }

            info.channels = new List<channelInfo>();
            for (int i = 0; i < (int)info.channelRecords.count; i++)
            {

                br.Stream.Position = (int)channelRecordStart + (int)info.channelRecords.references[i].offset;

                long adpcmInfoStartingOffset = br.Stream.Position;

                channelInfo c = new channelInfo();
                c.sampleRecord = new Reference();
                c.sampleRecord.identifier = br.ReadUInt16();
                c.sampleRecord.padding = br.ReadUInt16();
                c.sampleRecord.offset = br.ReadUInt32();
                c.adpcmRecord = new Reference();
                c.adpcmRecord.identifier = br.ReadUInt16();
                c.adpcmRecord.padding = br.ReadUInt16();
                c.adpcmRecord.offset = br.ReadUInt32();
                c.reserved = br.ReadUInt32();

                c.dspAdpcm = new dspAdpcmInfo();
                c.imaAdpcm = new imaAdpcmInfo();

                br.Stream.Position = (int)adpcmInfoStartingOffset + (int)c.adpcmRecord.offset;

                if (c.adpcmRecord.identifier == 0x0300)
                {

                    br.ReadInt16s(c.dspAdpcm.coefficients = new short[16]);
                    c.dspAdpcm.predScale = br.ReadUInt16();
                    c.dspAdpcm.yn1 = br.ReadUInt16();
                    c.dspAdpcm.yn2 = br.ReadUInt16();
                    c.dspAdpcm.loopPredScale = br.ReadUInt16();
                    c.dspAdpcm.loopYn1 = br.ReadUInt16();
                    c.dspAdpcm.loopYn2 = br.ReadUInt16();
                    c.dspAdpcm.padding = br.ReadUInt16();

                }
                else if (c.adpcmRecord.identifier == 0x0301)
                {

                    c.imaAdpcm.data = br.ReadUInt16();
                    c.imaAdpcm.tableIndex = br.ReadByte();
                    c.imaAdpcm.padding = br.ReadByte();
                    c.imaAdpcm.loopData = br.ReadUInt16();
                    c.imaAdpcm.loopTableIndex = br.ReadByte();
                    c.imaAdpcm.loopPadding = br.ReadByte();

                }

                info.channels.Add(c);

            }

            //Read reserved.
            int reservedSize = (int)info.size + headerSize - (int)br.Stream.Position;
            br.ReadBytes(reserved = new byte[reservedSize]);


            //Read data.
            br.Stream.Position = (int)dataRecord.r.offset;
            data.magic = br.ReadString_Count(4);
            data.size = br.ReadUInt32();
            br.ReadBytes(data.padding = new byte[(int)info.channels[0].sampleRecord.offset]);

            //Read samples.
            data.samples = new List<byte[]>();
            data.pcm16 = new List<ushort[]>();
            int size = (int)data.size - 8 - (int)info.channels[info.channels.Count - 1].sampleRecord.offset;
            for (int i = 0; i < (int)info.channelRecords.count; i++)
            {

                br.Stream.Position = (int)info.channels[i].sampleRecord.offset + 8 + (int)dataRecord.r.offset;

                if (info.soundEncoding != 1) { byte[] s = new byte[size]; br.ReadBytes(s); data.samples.Add(s); }
                else { UInt16[] p = new ushort[size / 2]; br.ReadUInt16s(p); data.pcm16.Add(p); }

            }

        }

        /// <summary>
        /// Convert to stream.
        /// </summary>
        /// <returns>The b stm.</returns>
        public b_stm toB_stm(b_wav h2 = null)
        {

            b_stm b = new b_stm();
            RIFF r;
            b_wav h;
            r = this.toRiff();
            h = r.toGameWav();
            b.numSamples = r.data.data.Length / ((r.fmt.bitsPerSample / 8) * r.fmt.numChannels);
            

            b.data = new b_stm.dataBlock();
            b.data.samples = h.data.samples;
            b.data.pcm16 = h.data.pcm16;

            //Info
            b.info.stream.encoding = h.info.soundEncoding;
            b.info.stream.loop = h.info.loop;
            b.info.stream.loopStart = h.info.loopStart;
            b.info.stream.loopEnd = h.info.loopEnd;
            b.info.stream.numberOfChannels = (byte)h.info.channels.Count;
            b.info.stream.numberOfRegions = 0;
            b.info.stream.sampleRate = h.info.samplingRate;

            if (b.info.stream.encoding != 2)
            {
                throw new NotImplementedException();
            }

            //Track
            b.info.track = new List<b_stm.infoBlock.trackInfo>();
            for (int i = 0; i < h.info.channels.Count; i += 2)
            {


                b_stm.infoBlock.trackInfo t = new b_stm.infoBlock.trackInfo();
                t.flags = 0;
                t.volume = 0x7F;
                t.pan = 0x40;
                t.byteTable = new b_stm.infoBlock.trackInfo.byteTableTrack();
                t.byteTable.count = 1;
                t.byteTable.channelIndexes = new List<byte>();
                t.byteTable.channelIndexes.Add((byte)i);

                if (i + 1 != (int)info.channels.Count)
                {

                    t.byteTable.count = 2;
                    t.byteTable.channelIndexes.Add((byte)(i + 1));

                }

                b.info.track.Add(t);

            }

            //Channel
            b.info.channel = new List<b_stm.infoBlock.channelInfo>();
            for (int i = 0; i < h.info.channels.Count; i++)
            {

                b_stm.infoBlock.channelInfo c = new b_stm.infoBlock.channelInfo();
                c.sampleRecord = new b_stm.Reference();
                c.dspAdpcm = new b_stm.infoBlock.dspAdpcmInfo();
                c.imaAdpcm = new b_stm.infoBlock.imaAdpcmInfo();

                c.dspAdpcm.coefficients = h.info.channels[i].dspAdpcm.coefficients;
                c.dspAdpcm.loopPredScale = h.info.channels[i].dspAdpcm.loopPredScale;
                c.dspAdpcm.loopYn1 = h.info.channels[i].dspAdpcm.loopYn1;
                c.dspAdpcm.loopYn2 = h.info.channels[i].dspAdpcm.loopYn2;
                c.dspAdpcm.padding = h.info.channels[i].dspAdpcm.padding;
                c.dspAdpcm.predScale = h.info.channels[i].dspAdpcm.predScale;
                c.dspAdpcm.yn1 = h.info.channels[i].dspAdpcm.yn1;
                c.dspAdpcm.yn2 = h.info.channels[i].dspAdpcm.yn2;

                c.imaAdpcm.data = h.info.channels[i].imaAdpcm.data;
                c.imaAdpcm.loopData = h.info.channels[i].imaAdpcm.loopData;
                c.imaAdpcm.loopPadding = h.info.channels[i].imaAdpcm.loopPadding;
                c.imaAdpcm.loopTableIndex = h.info.channels[i].imaAdpcm.loopTableIndex;
                c.imaAdpcm.padding = h.info.channels[i].imaAdpcm.padding;
                c.imaAdpcm.tableIndex = h.info.channels[i].imaAdpcm.tableIndex;

                b.info.channel.Add(c);

            }

            b.update(endianNess.big, true);


            return b;

        }

        /// <summary>
        /// Write to bytes.
        /// </summary>
        /// <param name="e"></param>
        public byte[] toBytes(endianNess e)
        {

            //Update.
            update(e);

            //Writer.
            MemoryStream o = new MemoryStream();
            EndianBinaryWriter bw = new EndianBinaryWriter(o);

            //Endianess.
            if (e == endianNess.big)
            {
                bw.Endianness = Endianness.BigEndian;
            }
            else
            {
                bw.Endianness = Endianness.LittleEndian;
            }

            //Write stuff.
            bw.WriteChars(magic);
            bw.WriteUInt16(byteOrder);

            //Other data.
            bw.WriteUInt16(headerSize);
            bw.WriteUInt32(version);
            bw.WriteUInt32(fileSize);
            bw.WriteUInt16(nBlocks);
            bw.WriteUInt16(padding);

            //Main references.
            bw.WriteUInt16(infoRecord.r.identifier);
            bw.WriteUInt16(infoRecord.r.padding);
            bw.WriteUInt32(infoRecord.r.offset);
            bw.WriteUInt32(infoRecord.size);

            bw.WriteUInt16(dataRecord.r.identifier);
            bw.WriteUInt16(dataRecord.r.padding);
            bw.WriteUInt32(dataRecord.r.offset);
            bw.WriteUInt32(dataRecord.size);
            bw.WriteBytes(reserved);

            //Info block.
            bw.WriteChars(info.magic);
            bw.WriteUInt32(info.size);
            bw.WriteByte(info.soundEncoding);
            bw.WriteByte(info.loop);
            bw.WriteUInt16(info.padding);
            bw.WriteUInt32(info.samplingRate);
            bw.WriteUInt32(info.loopStart);
            bw.WriteUInt32(info.loopEnd);
            bw.WriteUInt32(info.padding2);

            bw.WriteUInt32(info.channelRecords.count);
            for (int i = 0; i < (int)info.channelRecords.count; i++)
            {

                bw.WriteUInt16(info.channelRecords.references[i].identifier);
                bw.WriteUInt16(info.channelRecords.references[i].padding);
                bw.WriteUInt32(info.channelRecords.references[i].offset);

            }

            for (int i = 0; i < (int)info.channelRecords.count; i++)
            {

                bw.WriteUInt16(info.channels[i].sampleRecord.identifier);
                bw.WriteUInt16(info.channels[i].sampleRecord.padding);
                bw.WriteUInt32(info.channels[i].sampleRecord.offset);

                bw.WriteUInt16(info.channels[i].adpcmRecord.identifier);
                bw.WriteUInt16(info.channels[i].adpcmRecord.padding);
                bw.WriteUInt32(info.channels[i].adpcmRecord.offset);

                bw.WriteUInt32(info.channels[i].reserved);

                if (info.channels[i].adpcmRecord.identifier == 0x0300)
                {

                    bw.WriteInt16s(info.channels[i].dspAdpcm.coefficients);
                    bw.WriteUInt16(info.channels[i].dspAdpcm.predScale);
                    bw.WriteUInt16(info.channels[i].dspAdpcm.yn1);
                    bw.WriteUInt16(info.channels[i].dspAdpcm.yn2);
                    bw.WriteUInt16(info.channels[i].dspAdpcm.loopPredScale);
                    bw.WriteUInt16(info.channels[i].dspAdpcm.loopYn1);
                    bw.WriteUInt16(info.channels[i].dspAdpcm.loopYn2);
                    bw.WriteUInt16(info.channels[i].dspAdpcm.padding);

                }
                else if (info.channels[i].adpcmRecord.identifier == 0x0301)
                {

                    bw.WriteUInt16(info.channels[i].imaAdpcm.data);
                    bw.WriteByte(info.channels[i].imaAdpcm.tableIndex);
                    bw.WriteByte(info.channels[i].imaAdpcm.padding);
                    bw.WriteUInt16(info.channels[i].imaAdpcm.loopData);
                    bw.WriteByte(info.channels[i].imaAdpcm.loopTableIndex);
                    bw.WriteByte(info.channels[i].imaAdpcm.loopPadding);

                }

            }

            //Reserved.
            bw.WriteBytes(info.reserved);


            //Data.
            bw.WriteChars(data.magic);
            bw.WriteUInt32(data.size);
            bw.WriteBytes(data.padding);



            //Samples.
            for (int i = 0; i < (int)info.channels.Count; i++)
            {
                if (info.soundEncoding != 1) { bw.WriteBytes(data.samples[i]); }
                else { bw.WriteUInt16s(data.pcm16[i]); }
            }

            return o.ToArray();

        }



        /// <summary>
        /// Update.
        /// </summary>
        /// <param name="e"></param>
        public void update(endianNess e)
        {

            if (e == endianNess.big)
            {
                magic = "FWAV".ToString();
                version = 0x00010100;
            }
            else
            {
                magic = "CWAV".ToString();
                version = 0x02010000;
            }

            //Basic data.
            byteOrder = 0xFEFF;
            headerSize = 0x0040;
            fileSize = 0xFFFFFFFF;
            nBlocks = 2;
            padding = 0;

            infoRecord = new sizedReference();
            infoRecord.r = new Reference();
            infoRecord.r.identifier = 0x7000;
            infoRecord.r.padding = 0;
            infoRecord.r.offset = 0x0040;
            infoRecord.size = 0xFFFFFFFF;

            dataRecord = new sizedReference();
            dataRecord.r = new Reference();
            dataRecord.r.identifier = 0x7001;
            dataRecord.r.padding = 0;
            dataRecord.r.offset = 0xFFFF;
            dataRecord.size = 0xFFFFFFFF;

            reserved = new byte[0x14];

            //Will take care of sizes later.


            //Info.
            UInt32 infoSize = 32;
            info.magic = "INFO".ToString();
            info.size = 0xFFFFFFFF;
            info.padding = 0;
            info.padding2 = 0;


            //Channels
            info.channelRecords = new referenceTable();
            info.channelRecords.count = (UInt32)info.channels.Count;
            UInt32 channelOffsets = 4;
            info.channelRecords.references = new List<Reference>();
            for (int i = 0; i < (int)info.channelRecords.count; i++)
            {

                Reference r = new Reference();
                r.identifier = 0x7100;
                r.padding = 0;
                r.offset = 0xFFFFFFFF;
                info.channelRecords.references.Add(r);
                channelOffsets += 8;
                infoSize += 8;

            }

            for (int i = 0; i < info.channels.Count; i++)
            {

                Reference r = info.channelRecords.references[i];
                r.offset = (UInt32)channelOffsets;
                info.channelRecords.references[i] = r;

                channelInfo c = info.channels[i];

                c.sampleRecord = new Reference();
                c.sampleRecord.identifier = 0x1F00;
                c.sampleRecord.padding = 0;
                c.sampleRecord.offset = 0xFFFFFFFF;

                c.adpcmRecord = new Reference();

                if (info.soundEncoding == 2) { c.adpcmRecord.identifier = 0x0300; channelOffsets += 46; infoSize += 46; }
                else if (info.soundEncoding == 3) { c.adpcmRecord.identifier = 0x0301; channelOffsets += 8; infoSize += 8; }
                else { c.adpcmRecord.identifier = 0; }

                c.adpcmRecord.padding = 0;
                c.adpcmRecord.offset = 0xFFFFFFFF;
                if (info.soundEncoding == 2 || info.soundEncoding == 3) { c.adpcmRecord.offset = 0x14; }

                c.reserved = 0;

                c.dspAdpcm.padding = 0;
                c.imaAdpcm.padding = 0;
                c.imaAdpcm.loopPadding = 0;

                info.channels[i] = c;

                channelOffsets += 20;
                infoSize += 20;

            }


            //Reserved.
            List<byte> res = new List<byte>();
            while (infoSize % 0x20 != 0)
            {

                res.Add(0);
                infoSize += 1;

            }
            info.reserved = res.ToArray();
            info.size = infoSize;


            //Data.
            data.magic = "DATA".ToString();
            data.size = 0xFFFFFFFF;
            data.padding = new byte[0x18];

            UInt32 dataSize = 8 + 0x18;

            //Samples.
            UInt32 sampleOffsets = 0x18;
            for (int i = 0; i < (int)info.channels.Count; i++)
            {
                channelInfo c = info.channels[i];
                c.sampleRecord.offset = sampleOffsets;
                info.channels[i] = c;
                if (info.soundEncoding != 1)
                {
                    dataSize += (UInt32)data.samples[i].Length;
                    sampleOffsets += (UInt32)data.samples[i].Length;
                }
                else
                {
                    dataSize += (UInt32)data.pcm16[i].Length * 2;
                    sampleOffsets += (UInt32)data.pcm16[i].Length * 2;
                }
            }

            data.size = dataSize;

            //File size.
            fileSize = (UInt32)headerSize + info.size + data.size;

            //Write those offsets from earlier.
            infoRecord.size = info.size;
            dataRecord.size = data.size;
            dataRecord.r.offset = (UInt32)headerSize + info.size;

        }



        /// <summary>
        /// Convert to RIFF wave. ONLY SUPPORTS PCM B_WAVs!!!
        /// </summary>
        /// <returns></returns>
        public RIFF toRiff()
        {

            //Make new riff.
            RIFF r = new RIFF();

            //General data.
            r.fmt.chunkFormat = 1;
            r.fmt.numChannels = (UInt16)info.channels.Count;
            r.fmt.sampleRate = info.samplingRate;
            r.fmt.restOfData = new byte[0];


            //Different encoding related data:
            switch (info.soundEncoding)
            {

                //PCM8.
                case 0:

                    //Data block. A wave has each sample alternate, where Nintendo has the first channel then the other.
                    byte[][] sampleData = data.samples.ToArray();
                    MemoryStream waveSampleData = new MemoryStream();
                    BinaryWriter bw = new BinaryWriter(waveSampleData);
                    foreach (byte[] b in sampleData)
                    {
                        bw.Write(b);
                    }
                    r.data.data = waveSampleData.ToArray();

                    //Rest of fmt.
                    r.fmt.bitsPerSample = 8;
                    r.fmt.byteRate = r.fmt.sampleRate * r.fmt.numChannels * r.fmt.bitsPerSample / 8;
                    r.fmt.blockAlign = (UInt16)(r.fmt.numChannels * r.fmt.bitsPerSample / 8);

                    break;


                //PCM16.
                case 1:

                    //Data block.
                    UInt16[][] sampleData2 = data.pcm16.ToArray();
                    MemoryStream waveSampleData2 = new MemoryStream();
                    BinaryWriter bw2 = new BinaryWriter(waveSampleData2);
                    for (int i = 0; i < sampleData2[0].Length; i++)
                    {
                        foreach (UInt16[] b in sampleData2)
                        {
                            bw2.Write(b[i]);
                        }
                    }
                    r.data.data = waveSampleData2.ToArray();

                    //Rest of fmt.
                    r.fmt.bitsPerSample = 16;
                    r.fmt.byteRate = r.fmt.sampleRate * r.fmt.numChannels * r.fmt.bitsPerSample / 8;
                    r.fmt.blockAlign = (UInt16)(r.fmt.numChannels * r.fmt.bitsPerSample / 8);

                    break;


                //DSP-ADPCM Oh boy, here is the big one.
                case 2:

                    //Create the DSP blocks.
                    List<Dsp> dsps = new List<Dsp>();
                    for (int i = 0; i < info.channels.Count; i++)
                    {

                        Dsp d = new Dsp();
                        d.always2 = 2;
                        d.adpcmNibbles = (UInt32)(data.samples[i].Length * info.channels.Count);
                        d.blockFrameCount = 0;
                        d.channelCount = 0;
                        d.coefficients = info.channels[i].dspAdpcm.coefficients;
                        d.data = data.samples[i];
                        d.format = 0;
                        d.gain = 0;
                        d.loopEnd = d.adpcmNibbles - 1;
                        d.loopFlag = 0;
                        d.loopPredictor = info.channels[i].dspAdpcm.loopPredScale;
                        d.loopStart = 2;
                        d.loopYn1 = 0;
                        d.loopYn2 = 0;
                        d.numSamples = (UInt32)Math.Round(data.samples[0].Length * 2 * .8749999211, MidpointRounding.AwayFromZero);
                        d.padding = new UInt16[9];
                        d.predictor = info.channels[i].dspAdpcm.predScale;
                        d.sampleRate = info.samplingRate;
                        d.yn1 = info.channels[i].dspAdpcm.yn1;
                        d.yn2 = info.channels[i].dspAdpcm.yn2;

                        dsps.Add(d);

                    }

                    //Convert each DSP, and extract it's juicy new wave file.
                    List<RIFF> riffs = new List<RIFF>();
                    foreach (Dsp d in dsps)
                    {

                        Directory.SetCurrentDirectory(path + "\\Data\\Tools");
                        File.WriteAllBytes("tmp.dsp", d.toBytes());

                        Process p = new Process();
                        p.StartInfo.Arguments = "-D tmp.dsp tmp.wav";
                        p.StartInfo.CreateNoWindow = true;
                        p.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                        p.StartInfo.FileName = "dspadpcm.exe";
                        p.Start();
                        p.WaitForExit();

                        RIFF riff = new RIFF();
                        riff.load(File.ReadAllBytes("tmp.wav"));
                        riffs.Add(riff);

                        File.Delete("tmp.dsp");
                        File.Delete("tmp.wav");

                        /*
                        foreach (string s in Directory.EnumerateFiles(path + "Data\\Tools")) {
                            if (s.EndsWith(".txt")) { File.Delete(s); }
                        }
                        */
                        Directory.SetCurrentDirectory(path);


                    }

                    //Now that we have the RIFF(s), we can now write a SUPER-RIFF with all the channels.
                    r.fmt.bitsPerSample = 16;
                    r.fmt.byteRate = r.fmt.sampleRate * r.fmt.numChannels * r.fmt.bitsPerSample / 8;
                    r.fmt.blockAlign = (UInt16)(r.fmt.numChannels * r.fmt.bitsPerSample / 8);

                    //Here is the hard part, where we write each channel correctly.
                    MemoryStream newSampleData = new MemoryStream();
                    EndianBinaryWriter bw3 = new EndianBinaryWriter(newSampleData);

                    int byteCount = 0;
                    while (bw3.Stream.Position < riffs[0].data.data.Length * info.channels.Count)
                    {

                        foreach (RIFF f in riffs)
                        {
                            try {bw3.WriteByte(f.data.data[byteCount]); } catch { }
                            try {bw3.WriteByte(f.data.data[byteCount + 1]); } catch { }
                        }

                        byteCount += 2;

                    }

                    r.data.data = newSampleData.ToArray();

                    break;

            }

            //Return the final riff.
            return r;

        }


    }



    /// <summary>
    /// Warchive.
    /// </summary>
    public class b_war
    {

        public string magic; //FWAR; CWAR.
        public UInt16 byteOrder; //0xFEFF Big, 0xFFFE Little.
        public UInt16 headerSize; //Size of header.
        public UInt32 version; //0x00010000 for FWAR.
        public UInt32 fileSize; //File size.
        public UInt16 nBlocks; //Number of blocks, 2.
        public UInt16 padding; //Padding.

        public sizedReference infoReference; //INFO reference.
        public sizedReference fileReference; //FILE reference.

        public byte[] reserved; //Padding to make it aligned to the headerSize.

        public infoBlock info; //Info.
        public fileBlock file; //Files.


        /// <summary>
        /// Info block.
        /// </summary>
        public struct infoBlock
        {

            public string magic; //INFO.
            public UInt32 size; //Size.

            public sizedReferenceTable entries; //Entries.
            public byte[] reserved; //For 0x20 alignment.

        }


        /// <summary>
        /// File block.
        /// </summary>
        public struct fileBlock
        {

            public string magic; //FILE.
            public UInt32 size; //Size.
            public byte[] reserved; //Reserved.

            public List<fileEntry> files; //Files.

            /// <summary>
            /// File entry.
            /// </summary>
            public struct fileEntry
            {

                public byte[] file; //File.
                public byte[] padding; //To make it 0x20 aligned.

            }

        }


        /// <summary>
        /// Sized reference.
        /// </summary>
        public struct sizedReference
        {

            public UInt16 identifier; //Identifier.
                                      /*
                                       * 0x6800 - INFO.
                                       * 0x6801 - FILE.
                                       * 0x1F00 - INFO ENTRY.
                                       */
            public UInt16 padding; //Padding.
            public UInt32 offset; //Offset.
            public UInt32 size; //Size.

        }


        /// <summary>
        /// Sized reference table.
        /// </summary>
        public struct sizedReferenceTable
        {

            public UInt32 count; //Count.
            public List<sizedReference> references; //References.

        }


        /// <summary>
        /// Load the file.
        /// </summary>
        public void load(byte[] b)
        {

            //Reader.
            MemoryStream src = new MemoryStream(b);
            EndianBinaryReader br = new EndianBinaryReader(src);
            br.Endianness = Endianness.BigEndian;

            //Read stuff.
            magic = br.ReadString_Count(4);
            byteOrder = br.ReadUInt16();

            if (byteOrder == 0xFEFF)
            {
                br.Endianness = Endianness.BigEndian;
            }
            else
            {
                br.Endianness = Endianness.LittleEndian;
                byteOrder = 0xFEFF;
            }
            headerSize = br.ReadUInt16();
            version = br.ReadUInt32();
            fileSize = br.ReadUInt32();
            nBlocks = br.ReadUInt16();
            padding = br.ReadUInt16();

            infoReference = new sizedReference();
            infoReference.identifier = br.ReadUInt16();
            infoReference.padding = br.ReadUInt16();
            infoReference.offset = br.ReadUInt32();
            infoReference.size = br.ReadUInt32();

            fileReference = new sizedReference();
            fileReference.identifier = br.ReadUInt16();
            fileReference.padding = br.ReadUInt16();
            fileReference.offset = br.ReadUInt32();
            fileReference.size = br.ReadUInt32();

            br.ReadBytes(reserved = new byte[(int)headerSize - (int)br.Stream.Position]);

            //Info block.
            info = new infoBlock();
            br.Stream.Position = (int)infoReference.offset;

            info.magic = br.ReadString_Count(4);
            info.size = br.ReadUInt32();
            info.entries = new sizedReferenceTable();
            info.entries.count = br.ReadUInt32();
            info.entries.references = new List<sizedReference>();
            for (int i = 0; i < info.entries.count; i++)
            {

                sizedReference r = new sizedReference();
                r.identifier = br.ReadUInt16();
                r.padding = br.ReadUInt16();
                r.offset = br.ReadUInt32();
                r.size = br.ReadUInt32();
                info.entries.references.Add(r);

            }
            List<byte> reservedTemp = new List<byte>();
            int size = 12 + (int)info.entries.count * 12;
            while (size % 0x20 != 0)
            {
                reservedTemp.Add(0);
                size += 1;
            }
            info.reserved = reservedTemp.ToArray();

            //File block.
            file = new fileBlock();
            br.Stream.Position = (int)fileReference.offset;

            file.magic = br.ReadString_Count(4);
            file.size = br.ReadUInt32();
            int relOffset = (int)br.Stream.Position;
            br.ReadBytes(file.reserved = new byte[0x18]);
            file.files = new List<fileBlock.fileEntry>();
            foreach (sizedReference r in info.entries.references)
            {

                br.Stream.Position = relOffset + (int)(r.offset);
                fileBlock.fileEntry e = new fileBlock.fileEntry();
                br.ReadBytes(e.file = new byte[(int)r.size]);

                List<byte> paddingTemp = new List<byte>();
                int size2 = (int)r.size;
                while (size2 % 0x20 != 0)
                {
                    paddingTemp.Add(0);
                    size2 += 1;
                }
                e.padding = paddingTemp.ToArray();

                file.files.Add(e);

            }

        }


        /// <summary>
        /// Convert to bytes.
        /// </summary>
        /// <returns></returns>
        public byte[] toBytes(Endianness endian)
        {

            //Update.
            update(endian);


            //New writer.
            MemoryStream o = new MemoryStream();
            EndianBinaryWriter bw = new EndianBinaryWriter(o);
            bw.Endianness = endian;


            //Start writing.
            bw.WriteChars(magic);
            bw.WriteUInt16(byteOrder);
            bw.WriteUInt16(headerSize);
            bw.WriteUInt32(version);
            bw.WriteUInt32(fileSize);
            bw.WriteUInt16(nBlocks);
            bw.WriteUInt16(padding);

            bw.WriteUInt16(infoReference.identifier);
            bw.WriteUInt16(infoReference.padding);
            bw.WriteUInt32(infoReference.offset);
            bw.WriteUInt32(infoReference.size);

            bw.WriteUInt16(fileReference.identifier);
            bw.WriteUInt16(fileReference.padding);
            bw.WriteUInt32(fileReference.offset);
            bw.WriteUInt32(fileReference.size);

            bw.WriteBytes(reserved);


            //Info.
            bw.WriteChars(info.magic);
            bw.WriteUInt32(info.size);
            bw.WriteUInt32(info.entries.count);
            foreach (sizedReference r in info.entries.references)
            {
                bw.WriteUInt16(r.identifier);
                bw.WriteUInt16(r.padding);
                bw.WriteUInt32(r.offset);
                bw.WriteUInt32(r.size);
            }
            bw.WriteBytes(info.reserved);


            //File.
            bw.WriteChars(file.magic);
            bw.WriteUInt32(file.size);
            bw.WriteBytes(file.reserved);
            foreach (fileBlock.fileEntry e in file.files)
            {
                bw.WriteBytes(e.file);
                bw.WriteBytes(e.padding);
            }


            //Return new file.
            return o.ToArray();

        }


        /// <summary>
        /// Update the file.
        /// </summary>
        /// <param name="endian"></param>
        public void update(Endianness endian)
        {

            //General stuff.
            if (endian == Endianness.BigEndian)
            {
                magic = "FWAR".ToString();
            }
            else
            {
                magic = "CWAR".ToString();
            }

            for (int i = 0; i < file.files.Count; i++)
            {
                b_wav bW = new b_wav();
                bW.load(file.files[i].file);
                fileBlock.fileEntry e = file.files[i];
                if (endian == Endianness.BigEndian) { e.file = bW.toBytes(endianNess.big); } else { e.file = bW.toBytes(endianNess.little); }
                file.files[i] = e;
            }

            byteOrder = 0xFEFF;
            headerSize = 0x40;
            version = 0x00010000;
            fileSize = 0xFFFFFFFF;
            nBlocks = 2;
            padding = 0;

            infoReference = new sizedReference();
            infoReference.identifier = 0x6800;
            infoReference.padding = 0;
            infoReference.offset = 0x40;
            infoReference.size = 0xFFFFFFFF;

            fileReference = new sizedReference();
            fileReference.identifier = 0x6801;
            fileReference.padding = 0;
            fileReference.offset = 0xFFFFFFFF;
            fileReference.size = 0xFFFFFFFF;

            reserved = new byte[0x14];


            //Info.
            info.magic = "INFO".ToString();
            info.size = 0xFFFFFFFF;
            info.entries = new sizedReferenceTable();
            info.entries.references = new List<sizedReference>();
            UInt32 offset = 0x18;
            UInt32 filesSizes = 0;
            for (int i = 0; i < file.files.Count; i++)
            {

                fileBlock.fileEntry f = file.files[i];

                sizedReference s = new sizedReference();
                s.offset = offset;
                s.padding = 0;
                s.identifier = 0x1F00;
                s.size = (UInt32)f.file.Length;

                int size = (int)s.size;
                List<byte> reservedT = new List<byte>();
                while (size % 0x20 != 0)
                {
                    reservedT.Add(0);
                    size += 1;
                }
                f.padding = reservedT.ToArray();
                offset += s.size + (UInt32)reservedT.Count;
                filesSizes += s.size + (UInt32)reservedT.Count;
                file.files[i] = f;
                info.entries.references.Add(s);

            }
            info.entries.count = (UInt32)info.entries.references.Count;

            int newReserved = 12 + 12 * (int)info.entries.count;
            List<byte> newReserved2 = new List<byte>();
            while (newReserved % 0x20 != 0)
            {
                newReserved2.Add(0);
                newReserved += 1;
            }
            info.reserved = newReserved2.ToArray();
            info.size = (UInt32)newReserved;
            infoReference.size = info.size;

            //File.
            file.magic = "FILE".ToString();
            file.size = filesSizes + 8 + 0x18;
            file.reserved = new byte[0x18];
            fileReference.size = file.size;
            fileReference.offset = 0x40 + info.size;

            fileSize = 0x40 + info.size + file.size;

        }


        /// <summary>
        /// Extract to folder.
        /// </summary>
        /// <param name="path"></param>
        public void extract(string path, Endianness endian)
        {

            int id = 0;
            foreach (fileBlock.fileEntry file in file.files)
            {
                b_wav b = new b_wav();
                b.load(file.file);
                if (endian == Endianness.BigEndian)
                {
                    File.WriteAllBytes(path + "/" + id.ToString("D4") + ".bfwav", b.toBytes(endianNess.big));
                }
                else
                {
                    File.WriteAllBytes(path + "/" + id.ToString("D4") + ".bcwav", b.toBytes(endianNess.little));
                }
                id += 1;
            }

        }


        /// <summary>
        /// Compress path to file.
        /// </summary>
        /// <param name="path"></param>
        public void compress(string path)
        {

            string[] names = Directory.GetFiles(path);
            List<byte[]> files = new List<byte[]>();
            foreach (string name in names)
            {
                files.Add(File.ReadAllBytes(name));
            }

            file = new fileBlock();
            file.files = new List<fileBlock.fileEntry>();
            foreach (byte[] b in files)
            {

                fileBlock.fileEntry e = new fileBlock.fileEntry();
                e.file = b;
                e.padding = new byte[0];
                file.files.Add(e);

            }
            update(Endianness.BigEndian);

        }

    }

}

