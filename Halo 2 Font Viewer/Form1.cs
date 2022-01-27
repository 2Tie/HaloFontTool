using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Halo_2_Font_Viewer
{
    public partial class Form1 : Form
    {
        Graphics g;
        Image buffer;
        List<Bitmap> characters;

        public struct clickobject
        {
            public int x;
            public int y;
            public int width;
            public int height;
        }

        List<clickobject> clickobjects;

        public Form1()
        {
            InitializeComponent();
            Text = "Halo 2 font viewer";
            DoubleBuffered = true;
            Size = new Size(1600, 800); //biggun for MCC fonts
            buffer = new Bitmap(ClientSize.Width, ClientSize.Height);
            BackgroundImage = buffer;
            g = Graphics.FromImage(buffer);
            //conduit-9 is new to vista
            string filename = "";
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "Halo 2 font files|*-*|Halo 3 font files|font_package*.bin";
                openFileDialog.RestoreDirectory = true;

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    //Get the path of specified file
                    filename = openFileDialog.FileName;
                }
            }
            if (filename == "")
            {
                return;
            }
            if (filename.Contains("font_package") && filename.EndsWith(".bin"))
                LoadH3Font(filename);
            else
                LoadFont(filename);
        }

        public struct charHeader
        {
            public ushort dwidth; //?
            public ushort size;
            public ushort width;
            public ushort height;
            public ushort unk1; //type?
            public ushort unk2; //?
            public uint pointer;
        }

        public List<charHeader> charHeaders;

        public void LoadFont(string path)
        {
            System.Diagnostics.Debug.WriteLine("H2 font loading");
            charHeaders = new List<charHeader>();
            clickobjects = new List<clickobject>();
            g.Clear(Color.Black);
            using (BinaryReader br = new BinaryReader(File.OpenRead(path)))
            {
                br.BaseStream.Seek(0x20C, SeekOrigin.Begin);
                uint entries = br.ReadUInt32();
                br.BaseStream.Seek(0x40400, SeekOrigin.Begin);
                for(int i = 0; i < entries; i++)
                    charHeaders.Add(new charHeader { dwidth = br.ReadUInt16(), size = br.ReadUInt16(), width = br.ReadUInt16(), height = br.ReadUInt16(), unk1 = br.ReadUInt16(), unk2 = br.ReadUInt16(), pointer = br.ReadUInt32() });

                int x = 0;
                int y = 0;
                int maxY = 0;

                characters = new List<Bitmap>();

                //now we have our headers populated, let's take a look at the characters!
                for(int i = 0; i < charHeaders.Count; i++)
                {
                    //init an image for us
                    if (charHeaders[i].width == 0 || charHeaders[i].height == 0)
                        continue;
                    Bitmap c = new Bitmap(charHeaders[i].width, charHeaders[i].height);
                    BitmapData data = c.LockBits(new Rectangle(0, 0, charHeaders[i].width, charHeaders[i].height), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
                    br.BaseStream.Seek(charHeaders[i].pointer, SeekOrigin.Begin);
                    Color color = Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF);
                    unsafe
                    {
                        byte* ptr = (byte*)data.Scan0;
                        uint datasize = (uint)charHeaders[i].width * charHeaders[i].height;
                        for (int pos = 0; pos < charHeaders[i].size; pos++)
                        {
                            byte b = br.ReadByte();
                            if ((b & 0x80) == 0x80)
                            {
                                //special entry
                                //if C, two pixels of transparency using three bits each?
                                //unsure if this is totally correct though, stuff like entry 0x17 in bank 4 of iconsx3 (0x37510) seems to have transparency issues - reach weapon icons don't look to clean in larger versions either..
                                if ((b & 0x40) == 0x40)
                                {
                                    //ushort word = (ushort)((b << 8) | br.ReadByte());
                                    *(ptr++) = color.B;
                                    *(ptr++) = color.G;
                                    *(ptr++) = color.R;
                                    *(ptr++) = (byte)(((b << 2) & 0xE0));
                                    *(ptr++) = color.B;
                                    *(ptr++) = color.G;
                                    *(ptr++) = color.R; 
                                    *(ptr++) = (byte)(((b << 5) & 0xE0));
                                }
                                else
                                {
                                    //just a 0x8X/9X/AX/BX entry
                                    //first byte at specified alpha
                                    //color = Color.FromArgb((byte)((b << 2) & 0xE0), color);
                                    *(ptr++) = color.B;
                                    *(ptr++) = color.G;
                                    *(ptr++) = color.R;
                                    *(ptr++) = (byte)((b << 2) & 0xE0); //invert?
                                    //bitflag for full or empty
                                    bool full = ((b & 0x04) == 0x00);
                                    //then the rest of the number is a counter
                                    int counter = 5 - (b & (0x03));
                                    for (int r = 0; r < counter; r++)
                                    {
                                        if (ptr - (byte*)data.Scan0 >= datasize * 4)
                                            break;
                                        *(ptr++) = color.B;
                                        *(ptr++) = color.G;
                                        *(ptr++) = color.R;
                                        *(ptr++) = (byte)(full ? 0xFF : 0x00);
                                    }
                                    //color = Color.FromArgb(0xff, color);
                                }
                            }
                            else if ((b & 0x40) == 0x40)
                            {
                                b &= 0x3F;
                                //drawn run, using our last colour
                                color = Color.FromArgb(0xff, color);
                                for (int r = 0; r < b; r++)
                                {
                                    //if (pos + r > charHeaders[i].size)
                                    //    break;
                                    *(ptr++) = color.B;
                                    *(ptr++) = color.G;
                                    *(ptr++) = color.R;
                                    *(ptr++) = color.A;
                                }
                            }
                            else if (b == 0x00)
                            {
                                //set colour and draw 1
                                byte v1 = br.ReadByte();
                                byte v2 = br.ReadByte();
                                color = Color.FromArgb((v1&0xf0)|(v1>>4),((v1<<4)&0xf0)|(v1&0x0f),(v2&0xf0)|(v2>>4), ((v2<<4)&0xf0)|(0x0f));
                                *(ptr++) = color.B;
                                *(ptr++) = color.G;
                                *(ptr++) = color.R;
                                *(ptr++) = color.A;
                                pos += 2; //we read two bytes!
                            }
                            else
                            {
                                //undrawn run
                                for (int r = 0; r < b; r++)
                                {
                                    //if (pos + r > charHeaders[i].size)
                                    //    break;
                                    *(ptr++) = 00; //a
                                    *(ptr++) = 00; //r
                                    *(ptr++) = 00; //g
                                    *(ptr++) = 00; //b
                                }
                            }
                        }
                    }
                    c.UnlockBits(data);
                    //done writing our temp image, now copy it to the background?
                    if (x + charHeaders[i].width > ClientSize.Width)
                    {
                        x = 0;
                        y += maxY;
                        maxY = 0;
                    }
                    if (charHeaders[i].height > maxY)
                        maxY = charHeaders[i].height;
                    g.DrawImage(c, x, y, charHeaders[i].width, charHeaders[i].height);
                    characters.Add(c);
                    clickobjects.Add(new clickobject { x = x, y = y, width = charHeaders[i].width, height = charHeaders[i].height });
                    x += charHeaders[i].width;
                }
                //done loading data from the file

            }
            //file closed, let's display our canvas!
            g.Flush();
        }


        protected override void OnMouseClick(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right && clickobjects.Count != 0)
            {
                //check if over an item
                for (int i = 0; i < clickobjects.Count; i++)
                {
                    if (e.X >= clickobjects[i].x && e.Y >= clickobjects[i].y && e.X <= clickobjects[i].x + clickobjects[i].width && e.Y <= clickobjects[i].y + clickobjects[i].height)
                    {
                        //we're in the region, now let's save the image
                        System.Diagnostics.Debug.WriteLine("Saving image " + i.ToString("X"));
                        string dir = Directory.GetCurrentDirectory();
                        if (!Directory.Exists(dir + "/export/"))
                            Directory.CreateDirectory(dir + "/export/");
                        characters[i].Save(dir + "/export/" + i + ".png", ImageFormat.Png);
                    }
                }
            }
            base.OnMouseClick(e);
        }


        public class H3Font
        {
            public int headerposition;
            public int headersize;
            public string name;
            public uint page;
            public uint numChars;
            public List<char3Header> charHeaders;
            public List<Bitmap> characters;
        }

        public struct char3Header
        {
            public uint dwidth; //?
            public uint size;
            public ushort width;
            public ushort height;
            public ushort unk1; //type?
            public ushort unk2; //?
            public uint pointer;
        }

        public List<H3Font> H3Fonts;

        public void LoadH3Font(string path)
        {
            System.Diagnostics.Debug.WriteLine("H3 font loading");
            int which = 3; //for now, this selects which font to display - all the headers are processed but only required data gets drawn
            clickobjects = new List<clickobject>();
            H3Fonts = new List<H3Font>();
            g.Clear(Color.Black);
            using (BinaryReader br = new BinaryReader(File.OpenRead(path)))
            {
                //first, check quantity, make sure != 0
                br.BaseStream.Seek(0x4, SeekOrigin.Begin);
                uint fontsquantity = br.ReadUInt32();
                if (fontsquantity == 0)
                    throw new Exception("Zero fonts in this file!");
                //load the headers up
                for (int i = 0; i < fontsquantity; i++)
                {
                    H3Font font = new H3Font();
                    font.headerposition = br.ReadInt32();
                    font.headersize = br.ReadInt32();
                    font.charHeaders = new List<char3Header>();
                    br.ReadInt16(); //unknown
                    br.ReadInt16(); //unknown
                    H3Fonts.Add(font);
                    if (font.headerposition == 0)
                        throw new Exception("dummy font header detected!");
                }
                if (H3Fonts.Count == 0)
                    throw new Exception("empty font file!!");

                //get number of data banks
                br.BaseStream.Seek(0x414, SeekOrigin.Begin);
                uint banks = br.ReadUInt32();

                //load font headers
                for (int f = 0; f < fontsquantity; f++)
                {
                    //seek to font
                    br.BaseStream.Seek(H3Fonts[f].headerposition, SeekOrigin.Begin);
                    br.ReadUInt32(); //magic? version?
                    H3Fonts[f].name = new string(br.ReadChars(0x20)); //font name
                    br.BaseStream.Seek(0x118, SeekOrigin.Current); //seek ahead to characters quantity
                    H3Fonts[f].numChars = br.ReadUInt32();
                }


                for (int b = 0; b < banks; b++)
                {
                    int bankpos = (b + 1) * 0xC000; //halo 4 has them every 0x10000, 3 reach and odst 0xC000
                    br.BaseStream.Seek(bankpos, SeekOrigin.Begin); //travel to the character headers
                    br.ReadUInt16(); //unknown
                    uint charsquantity = br.ReadUInt16();
                    br.ReadUInt16(); //offset to data start
                    br.ReadUInt16(); //unknown
                    for (int i = 0; i < charsquantity; i++)
                    {
                        br.ReadUInt16(); //ascii code
                        int f = br.ReadUInt16(); //font
                        H3Fonts[f].charHeaders.Add(new char3Header { pointer = br.ReadUInt32() + (uint)bankpos }); //offset!
                    }
                }

                int x = 0;
                int y = 0;
                int maxY = 0;

                H3Fonts[which].characters = new List<Bitmap>();

                //now we have our headers populated, let's take a look at the characters!
                for (int i = 0; i < H3Fonts[which].charHeaders.Count; i++)
                {
                    //if (H3Fonts[which].charHeaders[i].pointer == 0x37510)
                    //    continue;
                    //jump to the character
                    br.BaseStream.Seek(H3Fonts[which].charHeaders[i].pointer, SeekOrigin.Begin);
                    //populate the rest of the character header
                    H3Fonts[which].charHeaders[i] = new char3Header { pointer = (uint)br.BaseStream.Position, dwidth = br.ReadUInt32(), size = br.ReadUInt32(), width = br.ReadUInt16(), height = br.ReadUInt16(), unk1 = br.ReadUInt16(), unk2 = br.ReadUInt16() };
                    //init an image for us
                    if (H3Fonts[which].charHeaders[i].width == 0 || H3Fonts[which].charHeaders[i].height == 0)
                        continue;
                    Bitmap c = new Bitmap(H3Fonts[which].charHeaders[i].width, H3Fonts[which].charHeaders[i].height);
                    BitmapData data = c.LockBits(new Rectangle(0, 0, H3Fonts[which].charHeaders[i].width, H3Fonts[which].charHeaders[i].height), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
                    Color color = Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF);
                    unsafe
                    {
                        byte* ptr = (byte*)data.Scan0;
                        uint datasize = (uint)H3Fonts[which].charHeaders[i].width * H3Fonts[which].charHeaders[i].height;
                        for (int pos = 0; pos < H3Fonts[which].charHeaders[i].size; pos++)
                        {
                            byte b = br.ReadByte();
                            //0xC0 out of 0x100 codes done
                            if ((b & 0x80) == 0x80)
                            {
                                //special entry
                                //if C, two pixels of transparency using three bits each?
                                if ((b & 0x40) == 0x40)
                                {
                                    //ushort word = (ushort)((b << 8) | br.ReadByte());
                                    *(ptr++) = color.B;
                                    *(ptr++) = color.G;
                                    *(ptr++) = color.R;
                                    *(ptr++) = (byte)(((b << 2) & 0xE0));
                                    *(ptr++) = color.B;
                                    *(ptr++) = color.G;
                                    *(ptr++) = color.R;
                                    *(ptr++) = (byte)(((b << 5) & 0xE0));
                                }
                                else
                                {
                                    //just a 0x8X/9X/AX/BX entry
                                    //first byte at specified alpha
                                    //color = Color.FromArgb((byte)((b << 2) & 0xE0), color);
                                    *(ptr++) = color.B;
                                    *(ptr++) = color.G;
                                    *(ptr++) = color.R;
                                    *(ptr++) = (byte)((b << 2) & 0xE0); //invert?
                                    //bitflag for full or empty
                                    bool full = ((b & 0x04) == 0x00);
                                    //then the rest of the number is a counter
                                    int counter = 5 - (b & (0x03));
                                    for (int r = 0; r < counter; r++)
                                    {
                                        if (ptr - (byte*)data.Scan0 >= datasize * 4)
                                            break;
                                        *(ptr++) = color.B;
                                        *(ptr++) = color.G;
                                        *(ptr++) = color.R;
                                        *(ptr++) = (byte)(full ? 0xFF : 0x00);
                                    }
                                    //color = Color.FromArgb(0xff, color);
                                }
                            }
                            else if ((b & 0x40) == 0x40)
                            {
                                b &= 0x3F;
                                //drawn run, using our last colour
                                color = Color.FromArgb(0xff, color);
                                for (int r = 0; r < b; r++)
                                {
                                    //if (pos + r > charHeaders[i].size)
                                    //    break;
                                    *(ptr++) = color.B;
                                    *(ptr++) = color.G;
                                    *(ptr++) = color.R;
                                    *(ptr++) = color.A;
                                }
                            }
                            else if (b == 0x00)
                            {
                                //set colour and draw 1
                                byte v1 = br.ReadByte();
                                byte v2 = br.ReadByte();
                                color = Color.FromArgb((v1 & 0xf0) | (v1 >> 4), ((v1 << 4) & 0xf0) | (v1 & 0x0f), (v2 & 0xf0) | (v2 >> 4), ((v2 << 4) & 0xf0) | (0x0f));
                                *(ptr++) = color.B;
                                *(ptr++) = color.G;
                                *(ptr++) = color.R;
                                *(ptr++) = color.A;
                                pos += 2; //we read two bytes!
                            }
                            else
                            {
                                //undrawn run
                                for (int r = 0; r < b; r++)
                                {
                                    //if (pos + r > charHeaders[i].size)
                                    //    break;
                                    *(ptr++) = 00; //a
                                    *(ptr++) = 00; //r
                                    *(ptr++) = 00; //g
                                    *(ptr++) = 00; //b
                                }
                            }
                        }
                    }
                    c.UnlockBits(data);
                    //done writing our temp image, now copy it to the background?
                    if (x + H3Fonts[which].charHeaders[i].width > ClientSize.Width)
                    {
                        x = 0;
                        y += maxY;
                        maxY = 0;
                    }
                    if (H3Fonts[which].charHeaders[i].height > maxY)
                        maxY = H3Fonts[which].charHeaders[i].height;
                    g.DrawImage(c, x, y, H3Fonts[which].charHeaders[i].width, H3Fonts[which].charHeaders[i].height);
                    clickobjects.Add(new clickobject { x = x, y = y, width = H3Fonts[which].charHeaders[i].width, height = H3Fonts[which].charHeaders[i].height });
                    H3Fonts[which].characters.Add(c);
                    x += H3Fonts[which].charHeaders[i].width;
                }
                //done loading data from the file

            }
            //file closed, let's display our canvas!
            g.Flush();
        }
    }
}
