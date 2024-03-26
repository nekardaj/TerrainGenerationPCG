using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NoiseLibrary;
using System.Drawing.Drawing2D;
using System.Numerics;
using Newtonsoft.Json;

namespace TerrainGenerationPCG
{
    public class Utils
    {
        /// <summary>
        /// Saves the noise to a grayscale image
        /// For testing purposes
        /// </summary>
        /// <param name="noise"></param>
        /// <param name="filename"></param>
        /// <param name="normalize">If true we assume -1 to 1 range and normalize it to 0-1</param>
        public static void SaveNoiseToGrayscaleImage(float[,] noise, string filename, bool normalize)
        {
            int width = noise.GetLength(0);
            int height = noise.GetLength(1);

            Bitmap bmp = new Bitmap(width, height);
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    byte val = 0;
                    if (normalize)
                    {
                        val = (byte)((noise[x, y] + 1) * 127);
                    }
                    else
                    {
                        val = (byte)(noise[x, y] * 255);
                    }
                    bmp.SetPixel(x, y, Color.FromArgb(val, val, val));
                }
            }
            bmp.Save(filename);
        }
        /// <summary>
        /// Saves heightmap to a grayscale image
        /// For testing purposes
        /// </summary>
        /// <param name="heightMap"></param>
        /// <param name="filename"></param>
        public static void SaveHeightmap(int[,] heightMap, string filename)
        {
            int width = heightMap.GetLength(0);
            int height = heightMap.GetLength(1);
            Bitmap bmp = new Bitmap(width, height);
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    byte val = (byte)(heightMap[x, y]);
                    bmp.SetPixel(x, y, Color.FromArgb(val, val, val));
                }
            }
            bmp.Save(filename);
        }

        // Stolen from Java's Color class
        public static float[] RGBtoHSB(int r, int g, int b)
        {
            float[] hsbvals = new float[3];
            float hue, saturation, brightness;
            int cmax = (r > g) ? r : g;
            if (b > cmax) cmax = b;
            int cmin = (r < g) ? r : g;
            if (b < cmin) cmin = b;

            brightness = ((float)cmax) / 255.0f;
            if (cmax != 0)
                saturation = ((float)(cmax - cmin)) / ((float)cmax);
            else
                saturation = 0;
            if (saturation == 0)
                hue = 0;
            else
            {
                float redc = ((float)(cmax - r)) / ((float)(cmax - cmin));
                float greenc = ((float)(cmax - g)) / ((float)(cmax - cmin));
                float bluec = ((float)(cmax - b)) / ((float)(cmax - cmin));
                if (r == cmax)
                    hue = bluec - greenc;
                else if (g == cmax)
                    hue = 2.0f + redc - bluec;
                else
                    hue = 4.0f + greenc - redc;
                hue = hue / 6.0f;
                if (hue < 0)
                    hue = hue + 1.0f;
            }
            hsbvals[0] = hue;
            hsbvals[1] = saturation;
            hsbvals[2] = brightness;
            return hsbvals;
        }
        // Stolen from Java's Color class
        public static float[] HSBtoRGB(float[] hsv)
        {
            float hue = hsv[0];
            float saturation = hsv[1];
            float brightness = hsv[2];
            int r = 0, g = 0, b = 0;
            if (saturation == 0)
            {
                r = g = b = (int)(brightness * 255.0f + 0.5f);
            }
            else
            {
                float h = (hue - (float)Math.Floor(hue)) * 6.0f;
                float f = h - (float)Math.Floor(h);
                float p = brightness * (1.0f - saturation);
                float q = brightness * (1.0f - saturation * f);
                float t = brightness * (1.0f - (saturation * (1.0f - f)));
                switch ((int)h)
                {
                    case 0:
                        r = (int)(brightness * 255.0f + 0.5f);
                        g = (int)(t * 255.0f + 0.5f);
                        b = (int)(p * 255.0f + 0.5f);
                        break;
                    case 1:
                        r = (int)(q * 255.0f + 0.5f);
                        g = (int)(brightness * 255.0f + 0.5f);
                        b = (int)(p * 255.0f + 0.5f);
                        break;
                    case 2:
                        r = (int)(p * 255.0f + 0.5f);
                        g = (int)(brightness * 255.0f + 0.5f);
                        b = (int)(t * 255.0f + 0.5f);
                        break;
                    case 3:
                        r = (int)(p * 255.0f + 0.5f);
                        g = (int)(q * 255.0f + 0.5f);
                        b = (int)(brightness * 255.0f + 0.5f);
                        break;
                    case 4:
                        r = (int)(t * 255.0f + 0.5f);
                        g = (int)(p * 255.0f + 0.5f);
                        b = (int)(brightness * 255.0f + 0.5f);
                        break;
                    case 5:
                        r = (int)(brightness * 255.0f + 0.5f);
                        g = (int)(p * 255.0f + 0.5f);
                        b = (int)(q * 255.0f + 0.5f);
                        break;
                }
            }
            return new float[] { r, g, b };
        }

        // save the noise in CSV format
        public static void SaveNoiseToCSV<T>(T[,] noise, string filename)
        {
            int width = noise.GetLength(0);
            int height = noise.GetLength(1);
            StringBuilder sb = new StringBuilder();
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    sb.Append(noise[x, y]);
                    if (y < height - 1)
                    {
                        sb.Append(",");
                    }
                }
                sb.AppendLine();
            }
            System.IO.File.WriteAllText(filename, sb.ToString());
        }

        public static FastNoiseLite CreateNoiseFromConfig(NoiseConfig config)
        {
            FastNoiseLite noise = new FastNoiseLite();
            noise.SetNoiseType(config.NoiseType);
            noise.SetFractalType(config.FractalType);
            noise.SetFractalOctaves(config.FractalOctaves);
            noise.SetFractalLacunarity(config.FractalLacunarity);
            noise.SetFractalGain(config.FractalGain);
            noise.SetFrequency(config.Frequency); // scale the frequency by the chunk size to make sure every biome occupies at least one chunk
            // I think this is better than the possibility of having a biome that is only few blocks big
            // noise.SetSeed(config.Seed);
            noise.SetSeed(System.DateTime.Now.Millisecond ^ 0b101001);
            noise.SetFractalWeightedStrength(config.FractalWeightedStrength);
            noise.SetFractalPingPongStrength(config.FractalPingPongStrength);
            return noise;
        }
        /// <summary>
        /// The library advises to use a separate instance for the configuration of the domain warp
        /// This allows having different settings for the domain warp and the noise itself
        /// </summary>
        public static FastNoiseLite CreateDomainWarpFromConfig(NoiseConfig config)
        {
            FastNoiseLite noise = new FastNoiseLite();
            noise.SetNoiseType(config.NoiseType);
            noise.SetDomainWarpType(config.DomainWarpType);
            noise.SetFractalType(FastNoiseLite.FractalType.DomainWarpProgressive);
            noise.SetFractalOctaves(config.DomainWarpOctaves);
            noise.SetDomainWarpAmp(config.DomainWarpAmp);
            noise.SetFrequency(config.DomainWarpFrequency);
            noise.SetSeed(config.Seed ^ 1010101);
            return noise;

        }

        internal static void CreateConfig(string filename)
        {
            NoiseConfig config = new NoiseConfig
            {
                NoiseType = FastNoiseLite.NoiseType.OpenSimplex2,
                FractalType = FastNoiseLite.FractalType.FBm,
                FractalOctaves = 3,
                FractalLacunarity = 2.0f,
                FractalGain = 0.5f,
                Frequency = 0.01f,
                Seed = 1337,
                FractalWeightedStrength = 0.0f,
                FractalPingPongStrength = 2.0f,
                DomainWarpType = FastNoiseLite.DomainWarpType.OpenSimplex2,
                DomainWarpFractalType = FastNoiseLite.FractalType.DomainWarpIndependent,
                DomainWarpAmp = 20.0f,
                DomainWarpOctaves = 3,
                DomainWarpFrequency = 0.1f
            };
            // make JsonConvert use newline formatting
            JsonSerializerSettings settings = new JsonSerializerSettings
            {
                Formatting = Formatting.Indented
            };
            System.IO.File.WriteAllText(filename, JsonConvert.SerializeObject(config, settings));
        }

        internal static void CreateBiomeConfig()
        {
            // to not overwrite the existing files, we will read the existing biomes and save them again
            // which adds new field if they were added keeping the old values
            for (int i = (int)0; i < (int)BiomeType.Count; i++)
            {
                Biome biome = JsonConvert.DeserializeObject<Biome>(System.IO.File.ReadAllText(((BiomeType)i).ToString()));
                JsonSerializerSettings settings = new JsonSerializerSettings
                {
                    Formatting = Formatting.Indented
                };
                System.IO.File.WriteAllText(biome.Type.ToString() + ".json", JsonConvert.SerializeObject(biome, settings));
            }
        }

        public static readonly Color[] BiomeColors = new Color[]
        {
            Color.White,
            Color.DarkGray,
            Color.DarkSlateGray,
            Color.DarkGreen,
            Color.LimeGreen,
            Color.Yellow,
            Color.DarkKhaki,
            Color.GreenYellow,
            Color.DarkRed,
            Color.DarkBlue,
            Color.DeepSkyBlue,
        };

        internal static void SaveBiomeMap(int width, int height)
        {
            WhittakerDiagram diagram = new WhittakerDiagram();

            Bitmap bmp = new Bitmap(width, height);
            int[,] heightMap = new int[width, height];
            BiomeType[,] biomeMap = new BiomeType[width, height];
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    float temperature = (float)(x) / width;
                    float precipitation = (float)y / height;
                    Biome biome = diagram.GetBiome(temperature, precipitation);
                    heightMap[x, y] = biome.BaseHeight;
                    biomeMap[x, y] = biome.Type;
                }
            }

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    float temperature = (float)(x) / width;
                    float precipitation = (float)y / height;
                    Biome biome = diagram.GetBiome(temperature, precipitation);
                    if (biome != null)
                    {
                        bmp.SetPixel(x, height - y - 1, BiomeColors[(int)biome.Type]);
                    }
                    else
                    {
                        bmp.SetPixel(x, height - y - 1, Color.Pink);
                    }
                }
            }

        }

        public static void GenerateBiomeHeightmap(BiomeType biome)
        {
            WhittakerDiagram diagram = new WhittakerDiagram();
            Biome b = diagram.GetBiome(biome);
            int width = 400;
            int height = 400;
            Bitmap bmp = new Bitmap(width, height);
            int[,] heightMap = new int[width, height];
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    heightMap[x, y] = b.GetHeight(x, y);
                }
            }
            SaveHeightmap(heightMap, "output\\" + biome.ToString() + ".png");
        }

        static Utils()
        {
            if (BiomeColors.Length != (int)BiomeType.Count)
            {
                throw new Exception("BiomeColors array must have the same length as the number of biomes");
            }
        }

        // testing function that creates map of precipitation x temperature -> biome
        public static void CreateBiomeMap()
        {
            WhittakerDiagram diagram = new WhittakerDiagram();
            Bitmap bmp = new Bitmap(400, 400);
            for (int x = 0; x < 400; x++)
            {
                for (int y = 0; y < 400; y++)
                {
                    float temperature = (float)(x) / 400;
                    float precipitation = (float)y / 400;
                    Biome biome = diagram.GetBiome(temperature, precipitation);
                    if (biome != null)
                    {
                        bmp.SetPixel(x, 399-y, BiomeColors[(int)biome.Type]);
                    }
                    else
                    {
                        bmp.SetPixel(x, 399-y, Color.Pink);
                    }
                }
            }
            bmp.Save("biomeMap.png");
        }

    }

    // configuration structure for noise generation
    // the structure will be deserialized from a JSON file
    // it hold data for both noise and its domain warp
    public class NoiseConfig
    {
        public FastNoiseLite.NoiseType NoiseType { get; set; }
        public FastNoiseLite.FractalType FractalType { get; set; }
        public int FractalOctaves { get; set; }
        public float FractalLacunarity { get; set; }
        public float FractalGain { get; set; }
        public float Frequency { get; set; }
        public int Seed { get; set; }
        public float FractalWeightedStrength { get; set; }
        public float FractalPingPongStrength { get; set; }
        public FastNoiseLite.DomainWarpType DomainWarpType { get; set; }
        public FastNoiseLite.FractalType DomainWarpFractalType { get; set; }
        public float DomainWarpAmp { get; set; }
        public int DomainWarpOctaves{ get; set;}
        public float DomainWarpFrequency { get; set; }
    }

}
