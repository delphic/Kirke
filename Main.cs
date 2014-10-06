using System;
using System.IO;
using System.Collections.Generic;
using System.Text;

namespace Kirke
{
	public class Frame
	{
		/// <summary>
		/// The x offset of the current frame
		/// </summary>
		public byte OffsetX;
		/// <summary>
		/// The y offset of the current frame
		/// </summary>
		public byte OffsetY;
		/// <summary>
		/// The width of the current frame
		/// </summary>
		public byte FrameWidth;
		/// <summary>
		/// The height of the current frame
		/// </summary>
		public byte FrameHeight;
		/// <summary>
		/// A global offset to the current frames Line Offsets
		/// </summary>
		public long DataOffset;
		/// <summary>
		/// The Palette Indices for the frame
		/// </summary>
		public byte[][] FrameData;

		public override string ToString()
		{
			return string.Format(
				"[FrameHeader: OffsetX={0}; OffsetY={1}, FrameWidth={2}, FrameHeight={3}, DataOffset={4}]",
				OffsetX, 
				OffsetY, 
			    FrameWidth, 
			    FrameHeight, 
			    DataOffset);
		}

		public string ToJsonString(string indent = "")
		{
			var sb = new StringBuilder();
			sb.Append(indent);
			sb.AppendLine("{");
			sb.Append(indent);
			sb.AppendLine(string.Format("\t\"offsetX\": {0},", OffsetX));
			sb.Append(indent);
			sb.AppendLine(string.Format("\t\"offsetY\": {0},", OffsetY));
			sb.Append(indent);
			sb.AppendLine(string.Format("\t\"frameWidth\": {0},", FrameWidth));
			sb.Append(indent);
			sb.AppendLine(string.Format("\t\"frameHeight\": {0},", FrameHeight));
			sb.Append(indent);
			sb.AppendLine("\t\"frameData\": [");
			for(int i = 0, l = FrameData.Length; i < l; i++)
			{
				sb.Append(indent);
				sb.Append("\t\t[ ");
				for(int j = 0, n = FrameData[i].Length; j < n; j++)
				{
					var numberString = FrameData[i][j].ToString();
					for(int k = 3 - numberString.Length, m = 0; k > m; k--)
					{
						sb.Append(" ");
					}
					sb.Append(FrameData[i][j]);
					if(j+1 != n) 
					{
						sb.Append(", ");
					}
				}
				sb.Append("]");
				if(i+1 != l)
				{
					sb.AppendLine(",");
				}
				else
				{
					sb.AppendLine();
				}
			}
			sb.Append(indent);
			sb.AppendLine("\t]");
			sb.Append(indent);
			sb.Append("}");
			return sb.ToString();
		}
	}

	public class Colour
	{
		public Colour(byte r, byte g, byte b)
		{
			R = r;
			G = g;
			B = b;
		}
		public byte R;
		public byte G;
		public byte B;

		public string ToJsonString(string indent)
		{
			return string.Format("{3}[{0}, {1}, {2}]", R, G, B, indent);
		}
	}

	class MainClass
	{
		public static void Main(string[] args)
		{
			if(args.Length < 1)
			{
				Console.WriteLine("Please provide a file path or directory or type \"kirke help\" for help!");
			}
			else if (args[0] == "help")
			{
				Console.WriteLine("Kirke converts .grp and .pal files to .json for debugging / prototyping / interest");
				Console.WriteLine();
				Console.WriteLine("kire <path to file>");
				Console.WriteLine();

			}
			else
			{
				// TODO: enumerate args, check if dir and if so decompress all, take output directory
				string filePath = args[0];
				string extension = Path.GetExtension(filePath);
				if(extension == ".grp")
				{
					string outputPath = Path.GetFileNameWithoutExtension(filePath) + ".json";
					File.WriteAllText(outputPath, ConvertGrpToJson(filePath));
					Console.WriteLine(string.Format("Converted {0} to {1}", Path.GetFileName(filePath), outputPath));
				}
				else if(extension == ".pal")
				{
					string outputPath = Path.GetFileNameWithoutExtension(filePath) + ".json";
					File.WriteAllText(outputPath, ConvertPalToJson(filePath));
					Console.WriteLine(string.Format("Converted {0} to {1}", Path.GetFileName(filePath), outputPath));
				}
				else
				{
					Console.WriteLine("Kirke can not convert files with an extension of: " + extension);
				}
			}
		}

		static string ConvertPalToJson(string path)
		{
			// Assumes StarCraft - non-text format
			// c.f. http://modcrafters.com/wiki/index.php?title=Palette_files
			var bytes = File.ReadAllBytes(path);
			int i = 0, l = bytes.Length;
			var colours = new Colour[l/3];
			while(i < l)
			{
				colours[i/3] = new Colour(bytes[i++], bytes[i++], bytes[i++]);
			}

			var sb = new StringBuilder();
			sb.AppendLine("{");
			sb.AppendLine("\t\"colours\": [");
			for(i = 0, l = colours.Length; i < l; i++)
			{
				sb.Append(colours[i].ToJsonString("\t\t"));
				if(i + 1 != l)
				{
					sb.AppendLine(",");
				}
				else
				{
					sb.AppendLine();
				}
			}
			sb.AppendLine("]}");
			return sb.ToString();
		}

		// TODO: Change to ParseGrp(string path) and return a Grp class 
		static string ConvertGrpToJson(string path)
		{			
			// Read .GRP File
			// c.f. http://modcrafters.com/wiki/index.php?title=GRP_files
			var bytes = File.ReadAllBytes(path);
			var fileName = Path.GetFileNameWithoutExtension(path);

			short frameCount = ToShort(bytes[0], bytes[1]);
			short groupWidth = ToShort(bytes[2], bytes[3]);
			short groupHeight = ToShort(bytes[4], bytes[5]);

			var frames = new Frame[frameCount];

			for(int i = 0; i < frameCount; i++)
			{
				long offset = i * 8;
				var header = new Frame {
					OffsetX = bytes[6 + offset],
					OffsetY = bytes[7 + offset],
					FrameWidth = bytes[8 + offset],
					FrameHeight = bytes[9 + offset],
					DataOffset = ToLong(bytes[10 + offset], bytes[11 + offset], bytes[12 + offset], bytes[13 + offset])
				};
				frames[i] = header;
			}

			foreach(var frame in frames)
			{
				var frameData = new byte[frame.FrameHeight][];
				var lineOffsets = new short[frame.FrameHeight];
				bool compressed = false;

				for(int i = 0, l = frame.FrameHeight; i < l; i++)
				{
					long offset = frame.DataOffset + 2 * i;
					lineOffsets[i] = ToShort(bytes[offset], bytes[offset + 1]);
					if(!compressed && i > 0)
					{
						compressed = (lineOffsets[i] - lineOffsets[i - 1]) < frame.FrameWidth;
					}
				}

				for(int i = 0, l = frame.FrameHeight; i < l; i++)
				{
					var lineData = new byte[frame.FrameWidth];
					if(!compressed)
					{
						for(int j = 0, m = lineData.Length; j < m; j++)
						{
							lineData[j] = bytes[frame.DataOffset + lineOffsets[i] + j];
						}
					}
					else
					{
						var offset = frame.DataOffset + lineOffsets[i];
						byte compSect;
						int bytesParsed = 0, currentIndex = 0;
						while(bytesParsed < lineData.Length)
						{
							compSect = bytes[offset + currentIndex++];
							if(compSect >= 0x80)
							{
								for(int j = 0, m = compSect - 0x80; j < m; j++)
								{
									if(bytesParsed >= lineData.Length)
									{
										break;
									}
									lineData[bytesParsed++] = 0x00; // Transparent
								}
							}
							else
								if(compSect >= 0x40)
								{
									var paletteIndex = bytes[offset + currentIndex++];
									for(int j = 0, m = compSect - 0x40; j < m; j++)
									{
										if(bytesParsed >= lineData.Length)
										{
											break;
										}
										lineData[bytesParsed++] = paletteIndex;
									}
								}
								else
								{
									for(int j = 0, m = compSect; j < m; j++)
									{
										if(bytesParsed >= lineData.Length)
										{
											break;
										}
										lineData[bytesParsed++] = bytes[offset + currentIndex++];
									}
								}
						}
					}
					frameData[i] = lineData;
				}
				frame.FrameData = frameData;
			}

			return ConvertToJson(fileName, frameCount, groupWidth, groupHeight, frames);
		}

		// TODO: Move to method on Grp Class
		static string ConvertToJson(string fileName, short frameCount, short groupWidth, short groupHeight, Frame[] frames)
		{
			var sb = new StringBuilder();
			sb.AppendLine("{");
			sb.AppendLine(string.Format("\t\"name\": \"{0}\",", fileName));
			sb.AppendLine(string.Format("\t\"frameCount\": {0},", frameCount));
			sb.AppendLine(string.Format("\t\"groupWidth\": {0},", groupWidth));
			sb.AppendLine(string.Format("\t\"groupHeight\": {0},", groupHeight));
			sb.AppendLine("\t\"frames\": [");
			for(int i = 0, l = frames.Length; i < l; i++)
			{
				sb.Append(frames[i].ToJsonString("\t\t"));
				if(i + 1 != l)
				{
					sb.AppendLine(",");
				}
				else
				{
					sb.AppendLine();
				}
			}
			sb.AppendLine("\t]");
			sb.AppendLine("}");
			return sb.ToString();
		}

		// Little Endian 
		// Should arguably use BitConverter and include BitConverter.IsLittleEndian
		static short ToShort(byte first, byte second)
		{
			short f = (short)first, s = (short)second;
			return (short)((s << 8) + f);
		}

		static long ToLong(byte first, byte second, byte third, byte fourth)
		{
			long result = fourth;
			result = (result << 8) + third;
			result = (result << 8) + second;
			result = (result << 8) + first;
			return result;
		}
	}
}
