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
        TemperatureConfig = JsonConvert.DeserializeObject<NoiseConfig>(System.IO.File.ReadAllText("TemperatureConfig.json"));
        PrecipationConfig = JsonConvert.DeserializeObject<NoiseConfig>(System.IO.File.ReadAllText("PrecipationConfig.json"));
        temperatureNoise = Utils.CreateNoiseFromConfig(TemperatureConfig);
        temperatureNoiseWarp = Utils.CreateDomainWarpFromConfig(TemperatureConfig);
        precipationNoise = Utils.CreateNoiseFromConfig(PrecipationConfig);
        precipationNoiseWarp = Utils.CreateDomainWarpFromConfig(PrecipationConfig);
        WhittakerDiagram = new WhittakerDiagram();
        // Biome borders are chunked so we need to set the frequency to the chunk size
        temperatureNoise.SetFrequency(TemperatureConfig.Frequency * ChunkSize);
        precipationNoise.SetFrequency(PrecipationConfig.Frequency * ChunkSize);
        temperatureNoiseWarp.SetFrequency(TemperatureConfig.Frequency * ChunkSize);
        precipationNoiseWarp.SetFrequency(PrecipationConfig.Frequency * ChunkSize);
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

                var xWarpTemp = (float)(x / ChunkSize);
                var yWarpTemp = (float)(y / ChunkSize);
                temperatureNoiseWarp.DomainWarp(ref xWarpTemp, ref yWarpTemp);
                
                var xWarpPrec = (float)(x / ChunkSize);
                var yWarpPrec = (float)(y / ChunkSize);
                precipationNoiseWarp.DomainWarp(ref xWarpPrec, ref yWarpPrec);
                
                temperature[x, y] = (temperatureNoise.GetNoise(xWarpTemp, yWarpTemp) + 1) / 2; // normalize to 0-1
                precipation[x, y] = (precipationNoise.GetNoise(xWarpPrec, yWarpPrec) + 1) / 2;

                //heights[x, y] = WhittakerDiagram.GetBiome(temperature[x, y], precipation[x, y]).GetHeight(x, y);
                biomes[x, y] = WhittakerDiagram.GetBiome(temperature[x, y], precipation[x, y]).Type;
                heights[x, y] = GetHeight(x,y, WhittakerDiagram.GetBiome(temperature[x, y], precipation[x, y]));
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
                hsv[2] *= ((((heights[x, y] - minHeight) / 256f) / ((maxHeight - minHeight) / 256f)) * 0.80f + 0.15f);
                //hsv[1] = hsv[1] * (((heights[x,y] - minHeight) / 256f) / ((maxHeight - minHeight) / 256f) + 1) / 2;
                var rgb = Utils.HSBtoRGB(hsv);

                bitmap.SetPixel(x, y, Color.FromArgb(255, (int)(rgb[0]), (int)(rgb[1]), (int)(rgb[2])));
            }
        }

        Utils.SaveNoiseToGrayscaleImage(temperature, $"output\\temperature_{index}.png", false);
        Utils.SaveNoiseToGrayscaleImage(precipation, $"output\\precipation_{index}.png", false);
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

    private const float displacementStep = 6;
    private static List<Vector2> filteringDisplacements = new List<Vector2>()
    {
        displacementStep * new Vector2(1,0), displacementStep * new Vector2(-1, 0) , displacementStep * new Vector2(0, 1) , displacementStep * new Vector2(0, -1),
        displacementStep * new Vector2(0.5f, 0.5f), displacementStep * new Vector2(-0.5f, 0.5f), displacementStep * new Vector2(0.5f, -0.5f), displacementStep * new Vector2(-0.5f, -0.5f)
    };
    public int GetHeight(int x, int y)
    {
        var xWarpTemp = (float) (x / ChunkSize);
        var yWarpTemp = (float) (y / ChunkSize);
        temperatureNoiseWarp.DomainWarp(ref xWarpTemp, ref yWarpTemp);
        var temperature = (temperatureNoise.GetNoise(xWarpTemp, yWarpTemp) + 1) / 2; // normalize to 0-1
        
        var xWarpPrec = (float)(x / ChunkSize);
        var yWarpPrec = (float)(y / ChunkSize);
        precipationNoiseWarp.DomainWarp(ref xWarpPrec, ref yWarpPrec);
        var precipation = (precipationNoise.GetNoise(xWarpPrec, yWarpPrec) + 1) / 2;

        // take a step into four directions from the current point
        // if the step is less than 8 units, at least two of the points will be in the same chunk
        // get the biomes for each of the points
        // find bioms of eight points that are equally distant from the current point

        // blend biomes
        // get the height of the biome
        // return the height
        var biome = WhittakerDiagram.GetBiome(temperature, precipation);
        var biomeHeight = biome.GetHeight(x, y);

        // filtering should be method

        for (int i = 0; i < filteringDisplacements.Count; i++)
        {
            xWarpTemp = (x + filteringDisplacements[i].X) / ChunkSize;
            yWarpTemp = (y + filteringDisplacements[i].Y) / ChunkSize;
            temperatureNoiseWarp.DomainWarp(ref xWarpTemp, ref yWarpTemp);
            var temperatureTemp = (temperatureNoise.GetNoise(xWarpTemp, yWarpTemp) + 1) / 2; // normalize to 0-1

            xWarpPrec = (x + filteringDisplacements[i].X) / ChunkSize;
            yWarpPrec = (y + filteringDisplacements[i].Y) / ChunkSize;
            precipationNoiseWarp.DomainWarp(ref xWarpTemp, ref yWarpTemp);
            var precipationTemp = (precipationNoise.GetNoise(xWarpPrec, yWarpPrec) + 1) / 2;

            biome = WhittakerDiagram.GetBiome(temperatureTemp, precipationTemp);
            biomeHeight += biome.GetHeight(x, y);
        }

        return biomeHeight / (filteringDisplacements.Count + 1); // +1 for the current point
    }

    /// <summary>
    /// Gets biome at the given point
    /// </summary>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <returns></returns>
    public Biome GetBiome(int x, int y)
    {
        var xWarpTemp = (float)(x / ChunkSize);
        var yWarpTemp = (float)(y / ChunkSize);
        temperatureNoiseWarp.DomainWarp(ref xWarpTemp, ref yWarpTemp);
        var temperature = (temperatureNoise.GetNoise(xWarpTemp, yWarpTemp) + 1) / 2; // normalize to 0-1

        var xWarpPrec = (float)(x / ChunkSize);
        var yWarpPrec = (float)(y / ChunkSize);
        precipationNoiseWarp.DomainWarp(ref xWarpPrec, ref yWarpPrec);
        var precipation = (precipationNoise.GetNoise(xWarpPrec, yWarpPrec) + 1) / 2;

        var biome = WhittakerDiagram.GetBiome(temperature, precipation);
        return biome;
    }
    /// <summary>
    /// Gets the height at the given point using known biome
    /// Biomes are chunked so when we are constructing the chunk we only need to get the biome once
    /// then we can reuse the biome for all the points in the chunk
    /// </summary>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <param name="biome"></param>
    /// <returns></returns>
    public int GetHeight(int x, int y, Biome biome)
    {
        // take a step into four directions from the current point
        // get the biomes for each of the points
        // we ignore points in the same biome

        // simple filtering does not work
        
        var biomeHeight = 2 * biome.GetHeight(x, y);

        int filteringCount = 2;
        // blend biomes

        float xWarpTemp;
        float yWarpTemp;
        float xWarpPrec;
        float yWarpPrec;
        for (int i = 0; i < filteringDisplacements.Count; i++)
        {
            xWarpTemp = ((float)x / ChunkSize + filteringDisplacements[i].X);
            yWarpTemp = ((float)y / ChunkSize + filteringDisplacements[i].Y);
            temperatureNoiseWarp.DomainWarp(ref xWarpTemp, ref yWarpTemp);
            var temperatureTemp = (temperatureNoise.GetNoise(xWarpTemp, yWarpTemp) + 1) / 2; // normalize to 0-1

            xWarpPrec = ((float)x / ChunkSize + filteringDisplacements[i].X);
            yWarpPrec = ((float)y / ChunkSize + filteringDisplacements[i].Y);
            precipationNoiseWarp.DomainWarp(ref xWarpTemp, ref yWarpTemp);
            var precipationTemp = (precipationNoise.GetNoise(xWarpPrec, yWarpPrec) + 1) / 2;

            var currBiome = WhittakerDiagram.GetBiome(temperatureTemp, precipationTemp);
            if (currBiome.Type != biome.Type)
            {
                biomeHeight += currBiome.GetHeight(x, y);
                filteringCount++;
            }
        }

        return biomeHeight / filteringCount; // +1 for the current point

    }
}