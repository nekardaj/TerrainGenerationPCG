using System.Drawing;
using System.Globalization;
using System.Numerics;
using Newtonsoft.Json;
using NoiseLibrary;

namespace TerrainGenerationPCG;

public class MapGenerator
{
    public NoiseConfig TemperatureConfig;
    public NoiseConfig PrecipationConfig;
    public WhittakerDiagram WhittakerDiagram;

    private FastNoiseLite temperatureNoise;
    private FastNoiseLite temperatureNoiseWarp;
    private FastNoiseLite precipationNoise;
    private FastNoiseLite precipationNoiseWarp;

    public const int ChunkSize = 16;

    public MapGenerator()
    {
        // Deserialize noise configs from JSON files and create instances of noise generators
        // use path relative to this assembly
        string path = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);

        TemperatureConfig = JsonConvert.DeserializeObject<NoiseConfig>(System.IO.File.ReadAllText(path + "\\TemperatureConfig.json"));
        PrecipationConfig = JsonConvert.DeserializeObject<NoiseConfig>(System.IO.File.ReadAllText(path + "\\PrecipationConfig.json"));
        temperatureNoise = Utils.CreateNoiseFromConfig(TemperatureConfig);
        temperatureNoiseWarp = Utils.CreateDomainWarpFromConfig(TemperatureConfig);
        precipationNoise = Utils.CreateNoiseFromConfig(PrecipationConfig);
        precipationNoiseWarp = Utils.CreateDomainWarpFromConfig(PrecipationConfig);
        WhittakerDiagram = new WhittakerDiagram();
        // Biome borders are chunked so we need to set the frequency to the chunk size
        //temperatureNoise.SetFrequency(TemperatureConfig.Frequency * ChunkSize);
        //precipationNoise.SetFrequency(PrecipationConfig.Frequency * ChunkSize);
        //temperatureNoiseWarp.SetFrequency(TemperatureConfig.Frequency * ChunkSize);
        //precipationNoiseWarp.SetFrequency(PrecipationConfig.Frequency * ChunkSize);
    }

    internal void GenerateBiomeMap(int width, int height, int index)
    {
        float[,] temperature = new float[width, height];
        float[,] precipation = new float[width, height];
        int[,] heights = new int[width, height];

        Bitmap bitmap = new Bitmap(width, height);

        int minHeight = 0;
        int maxHeight = 255;

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                var xWarp = (float)x;
                var yWarp = (float)y;
                temperatureNoiseWarp.DomainWarp(ref xWarp, ref yWarp);
                
                temperature[x, y] = (temperatureNoise.GetNoise(xWarp, yWarp) + 1) / 2; // normalize to 0-1
                xWarp = (float)x;
                yWarp = (float)y;
                precipationNoiseWarp.DomainWarp(ref xWarp, ref yWarp);
                precipation[x, y] = (precipationNoise.GetNoise(xWarp, yWarp) + 1) / 2;
                //heights[x, y] = WhittakerDiagram.GetBiome(temperature[x, y], precipation[x,y]).GetHeight(x,y);
                heights[x, y] = 0;
                minHeight = Math.Min(minHeight, heights[x, y]);
                maxHeight = Math.Max(maxHeight, heights[x, y]);
            }
        }

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Biome biome = WhittakerDiagram.GetBiome(temperature[x, y], precipation[x, y]);
                Color color = Utils.BiomeColors[(int)biome.Type];

                // convert to HSL and modulate based on height
                var hsv = Utils.RGBtoHSB(color.R, color.G, color.B);
                // map to 0-1 and then remap
                hsv[2] = hsv[2] * ((((heights[x,y] - minHeight) / 256f) / ((maxHeight - minHeight) / 256f)) * 0.80f + 0.20f);
                //hsv[1] = hsv[1] * (((heights[x,y] - minHeight) / 256f) / ((maxHeight - minHeight) / 256f) + 1) / 2;
                var rgb = Utils.HSBtoRGB(hsv);
                
                bitmap.SetPixel(x, y, Color.FromArgb(255, (int)(rgb[0]), (int)(rgb[1]), (int)(rgb[2])));
            }
        }

        Utils.SaveNoiseToGrayscaleImage(temperature, $"output\\temperature_{index}.png", false);
        Utils.SaveNoiseToGrayscaleImage(precipation, $"output\\precipation_{index}.png", false);
        Utils.SaveHeightmap(heights, $"output\\heights_{index}.png");

        bitmap.Save($"output\\biome_{index}.png");
        Console.WriteLine("Biome image saved.");
        // print min and max precipation and temperature
        Console.WriteLine($"Temperature: {temperature.Cast<float>().Min()} {temperature.Cast<float>().Max()}");
        Console.WriteLine($"Precipation: {precipation.Cast<float>().Min()} {precipation.Cast<float>().Max()}");
    }
    /// <summary>
    /// Generates map divided into chunks with generated biomes
    /// Chunked biomes are used to generate heightmap
    /// </summary>
    /// <param name="width"></param>
    /// <param name="height"></param>
    /// <param name="index"></param>
    internal void GenerateHeightMap(int width, int height, int index)
    {
        float[,] temperature = new float[width, height];
        float[,] precipation = new float[width, height];
        int[,] heights = new int[width, height];
        BiomeType[,] biomes = new BiomeType[width, height];

        Bitmap bitmap = new Bitmap(width, height);

        int minHeight = 0;
        int maxHeight = 255;


        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {

                GetFilteredBiomeAndHeight(x,y).Deconstruct(out biomes[x,y], out heights[x,y]);

                minHeight = Math.Min(minHeight, heights[x, y]);
                maxHeight = Math.Max(maxHeight, heights[x, y]);
            }
        }

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Biome biome = WhittakerDiagram.GetBiome(biomes[x,y]);
                Color color = Utils.BiomeColors[(int)biome.Type];

                // convert to HSL and modulate based on height
                var hsv = Utils.RGBtoHSB(color.R, color.G, color.B);
                // map to 0-1 and then remap to 0.5-1
                hsv[2] *= ((((heights[x, y] - minHeight) / 256f) / ((maxHeight - minHeight) / 256f)) * 0.80f + 0.20f);
                //hsv[1] = hsv[1] * (((heights[x,y] - minHeight) / 256f) / ((maxHeight - minHeight) / 256f) + 1) / 2;
                var rgb = Utils.HSBtoRGB(hsv);

                bitmap.SetPixel(x, y, Color.FromArgb(255, (int)(rgb[0]), (int)(rgb[1]), (int)(rgb[2])));
            }
        }

        //Utils.SaveNoiseToGrayscaleImage(temperature, $"output\\temperature_{index}.png", false);
        //Utils.SaveNoiseToGrayscaleImage(precipation, $"output\\precipation_{index}.png", false);
        Utils.SaveHeightmap(heights, $"output\\heights_{index}.png");
        Utils.SaveNoiseToCSV(heights, $"output\\heights_csv_{index}.csv");

        bitmap.Save($"output\\biome_{index}.png");
        Console.WriteLine("Biome image saved.");
        // print min and max precipation and temperature
        Console.WriteLine($"Temperature: {temperature.Cast<float>().Min()} {temperature.Cast<float>().Max()}");
        Console.WriteLine($"Precipation: {precipation.Cast<float>().Min()} {precipation.Cast<float>().Max()}");

        // find discontinuities in the heightmap and print them
        // find the points where the height changes too fast
        // print the points and the height difference
        int threshold = 8;
        int discontinuities = 0;
        int[,] discontinuetiesPairs = new int[(int)BiomeType.Count, (int)BiomeType.Count];
        for (int x = 1; x < width; x++)
        {
            for (int y = 1; y < height; y++)
            {
                if (Math.Abs(heights[x, y] - heights[x - 1, y]) > 8)
                {
                    discontinuities++;
                    discontinuetiesPairs[(int)biomes[x, y], (int)biomes[x - 1, y]]++;
                }

                if (Math.Abs(heights[x, y] - heights[x, y - 1]) > 8)
                {
                    discontinuities++;
                    discontinuetiesPairs[(int)biomes[x, y], (int)biomes[x, y - 1]]++;
                }
            }
        }
        Console.WriteLine($"Discontinuities: {discontinuities}");
        for (int i = 0; i < (int)BiomeType.Count; i++)
        {
            for (int j = i; j < (int)BiomeType.Count; j++)
            {
                if (discontinuetiesPairs[i, j] > 0)
                {
                    Console.WriteLine($"{(BiomeType)i} {(BiomeType)j} {(int)discontinuetiesPairs[i, j] + (int)discontinuetiesPairs[j,i]}");
                }
            }
        }
    }

    private const float displacementStep = 5;
    private static List<Vector2> filteringDisplacements = new List<Vector2>()
    {
        displacementStep * new Vector2(1,0), displacementStep * new Vector2(-1, 0) , displacementStep * new Vector2(0, 1) , displacementStep * new Vector2(0, -1),
        displacementStep * new Vector2(0.5f, 0.5f), displacementStep * new Vector2(-0.5f, 0.5f), displacementStep * new Vector2(0.5f, -0.5f), displacementStep * new Vector2(-0.5f, -0.5f)
    };
    /// <summary>
    /// Computes the biome and height at the given point
    /// Since the biomes and heights are blended and multiple points are used to compute the both there is little benefit in keeping them separate
    /// </summary>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <returns>Computed biome and height of a given point</returns>
    public Tuple<BiomeType, int> GetFilteredBiomeAndHeight(int x, int y)
    {
        // blend biomes

        float xWarpTemp = x;
        float yWarpTemp = y;
        temperatureNoiseWarp.DomainWarp(ref xWarpTemp,ref yWarpTemp);
        float xWarpPrec = x;
        float yWarpPrec = y;
        precipationNoiseWarp.DomainWarp(ref xWarpPrec, ref yWarpPrec);

        // points used in filtering vote for the biome type
        int[] BiomeTypeVotes = new int[(int)BiomeType.Count];

        var biome = WhittakerDiagram.GetBiome((temperatureNoise.GetNoise(xWarpTemp, yWarpTemp) + 1) / 2, (precipationNoise.GetNoise(xWarpPrec, yWarpPrec) + 1) / 2);
        BiomeTypeVotes[(int)biome.Type]++;

        int filteringCount = 1;
        
        var biomeHeight = biome.GetHeight(x, y);
        for (int i = 0; i < filteringDisplacements.Count; i++)
        {
            xWarpTemp = ((float)x + filteringDisplacements[i].X);
            yWarpTemp = ((float)y + filteringDisplacements[i].Y);
            temperatureNoiseWarp.DomainWarp(ref xWarpTemp, ref yWarpTemp);
            var temperatureTemp = (temperatureNoise.GetNoise(xWarpTemp, yWarpTemp) + 1) / 2; // normalize to 0-1

            xWarpPrec = ((float)x + filteringDisplacements[i].X);
            yWarpPrec = ((float)y + filteringDisplacements[i].Y);
            precipationNoiseWarp.DomainWarp(ref xWarpTemp, ref yWarpTemp);
            var precipationTemp = (precipationNoise.GetNoise(xWarpPrec, yWarpPrec) + 1) / 2;

            biome = WhittakerDiagram.GetBiome(temperatureTemp, precipationTemp);
            biomeHeight += biome.GetHeight(x, y);
            filteringCount++;
        }
        // return biome with the most votes and filtered height
        int maxVotes = 0;
        int maxIndex = 0;
        for (int i = 0; i < BiomeTypeVotes.Length; i++)
        {
            if (BiomeTypeVotes[i] > maxVotes)
            {
                maxVotes = BiomeTypeVotes[i];
                maxIndex = i;
            }
        }
        return new Tuple<BiomeType, int>((BiomeType)maxIndex, biomeHeight / filteringCount);
    }
}