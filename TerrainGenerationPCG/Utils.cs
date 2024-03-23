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
        //save the noise to a file as a grayscale image in PNG format
        public static void SaveNoiseToImage(float[,] noise, string filename)
        {
            int width = noise.GetLength(0);
            int height = noise.GetLength(1);
            
            Bitmap bmp = new Bitmap(width, height);
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    byte val = (byte)((noise[x, y] + 1) * 127);
                    bmp.SetPixel(x, y, Color.FromArgb(val, val, val));
                }
            }
            bmp.Save(filename);
        }

        // save the noise in CSV format
        public static void SaveNoiseToCSV(float[,] noise, string filename)
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
            noise.SetFrequency(config.Frequency);
            noise.SetSeed(config.Seed);
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
            noise.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2);
            noise.SetDomainWarpType(FastNoiseLite.DomainWarpType.OpenSimplex2);
            noise.SetFractalOctaves(3);
            noise.SetDomainWarpAmp(30.0f);
            noise.SetFrequency(0.1f);
            noise.SetSeed(config.Seed);
            return noise;

        }

        public static void CreateConfig(string filename)
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

        public static void CreateBiomeConfig()
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
            Color.LightGray,
            Color.DarkSlateGray,
            Color.ForestGreen,
            Color.LimeGreen,
            Color.Yellow,
            Color.DarkKhaki,
            Color.DarkGreen,
            Color.DarkRed,
        };

        public static void SaveMap(int width, int height)
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
                    Color color = BiomeColors[(int)biome.Type];
                    // convert color to HSB
                    float hue = color.GetHue();
                    float saturation = color.GetSaturation();
                    float brightness = color.GetBrightness();
                    // modify brightness based on the height

                    if (biome != null)
                    {
                        bmp.SetPixel(x, 399 - y, BiomeColors[(int)biome.Type]);
                    }
                    else
                    {
                        bmp.SetPixel(x, 399 - y, Color.Pink);
                    }
                }
            }

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
