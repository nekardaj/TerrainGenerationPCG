using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NoiseLibrary;

namespace TerrainGenerationPCG
{
    public enum BiomeType
    {
        Tundra, // low temperature, low humidity
        Taiga, // low temperature, mid humidity
        TemperateGrassland, // mid temperature, low humidity, - few trees
        TemperateDeciduousForest, // mid temperature, mid humidity, - broadleaf forest
        TropicalSeasonalForest, // mid temperature, high humidity, - monsoon forest, - tropical seasonal forest
        Desert, // high temperature, low humidity
        Savanna, // high temperature, mid humidity
        TropicalRainforest, // high temperature, high humidity
        Mountain, // high temperature, very low humidity - high altitude
        ColdOcean, // water
        WarmOcean, // water
        Count
    }

    // create a representation of a Whittaker diagram
    // a class that recieves temperature and humidity and returns a biome
    // the class should be able to read a JSON file with the biomes and their temperature and humidity ranges
    // the class should be able to return a biome based on the temperature and humidity
    // It should support curved lines, not just straight lines
    // each biom should be represented by a polygon
    public class WhittakerDiagram
    {
        private List<Biome> biomes;

        public WhittakerDiagram()
        {
            string path = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            biomes = new List<Biome>();
            for (int i = 0; i < (int)BiomeType.Count; i++)
            {

                biomes.Add(JsonConvert.DeserializeObject<Biome>(System.IO.File.ReadAllText(path + "\\"+((BiomeType)i).ToString() + ".json")));
            }
        }

        public Biome GetBiome(BiomeType biome)
        {
            return biomes[(int)biome];
        }

        public Biome GetBiome(float temperature, float humidity)
        {
            // find the first biome that contains the point
            // if none is found, return the closest biome
            foreach (Biome biome in biomes)
            {
                if (biome.InsideBiome(new Vector2(temperature, humidity)))
                {
                    return biome;
                }
            }
            // if no biome is found, return the closest one
            Biome closestBiome = biomes[0];
            float minDistance = float.MaxValue;
            foreach (Biome biome in biomes)
            {
                float distance = Vector2.Distance(biome.center, new Vector2(temperature, humidity));
                if (distance < minDistance)
                {
                    minDistance = distance;
                    closestBiome = biome;
                }
            }
            return closestBiome;
        }

        public void PrintBiomeConfigs()
        {
            for (int i = 0; i < biomes.Count; i++)
            {
                Console.WriteLine(biomes[i].Type+ " base: " + biomes[i].BaseHeight + " amp: " + biomes[i].HeightAmplitude);
            }
        }
    }

    /// <summary>
    /// Biome data class
    /// Every biome will be represented by a convex polygon
    /// </summary>
    public class Biome
    {
        public string Name { get; set; }
        public BiomeType Type { get; set; }
        public int BaseHeight;
        public int HeightAmplitude;
        // temperature and humidity ranges
        // convex polygon of points, they must be ordered clockwise or counterclockwise
        public List<Vector2> points;
        public NoiseConfig noiseConfig;
        private FastNoiseLite noise;
        private FastNoiseLite noiseWarp;

        // as a backup we can store the center of the polygon
        // if point does not belong to any polygon, we can check the distance to the center and choose the closest polygon
        [JsonIgnore]
        public Vector2 center { get; protected set; }
        // bounding box for acceleration
        private float MinX;
        private float MaxX;
        private float MinY;
        private float MaxY;


        public Biome(string name, BiomeType type, List<Vector2> points)
        {
            Name = name;
            Type = type;
            this.points = points;
        }
        /// <summary>
        /// Every biome has own config of noise. It will be created from JSON file
        /// Since <see cref="InsideBiome"/> is expected to be called many times, we precompute the center and bounding box of the polygon to speed up the process
        /// This method is called only once so performance is not affected
        /// </summary>
        [OnDeserialized]
        public void OnDeserialized(StreamingContext context)
        {
            noise = Utils.CreateNoiseFromConfig(noiseConfig);
            noiseWarp = Utils.CreateDomainWarpFromConfig(noiseConfig);
            center = new Vector2(points.Average(p => p.X), points.Average(p => p.Y));
            MinX = points.Min(p => p.X);
            MaxX = points.Max(p => p.X);
            MinY = points.Min(p => p.Y);
            MaxY = points.Max(p => p.Y);
        }

        /// <summary>
        /// Checks whether a given point is inside the biome bounded by the polygon
        /// </summary>
        /// <param name="point"></param>
        /// <returns>True if point is inside the polygon</returns>
        public bool InsideBiome(Vector2 point)
        {
            // check if the point is inside the bounding box
            if (point.X < MinX || point.X > MaxX || point.Y < MinY || point.Y > MaxY)
            {
                return false;
            }
            int i;
            int left = 0;
            int right = 0;

            for (i = 0; i < points.Count - 1; i++)
            {
                // cross product between the point and the line
                // if the point is to the left of the line, the cross product will be positive
                // if the point is to the right of the line, the cross product will be negative

                // if the point is on the line, the cross product will be zero - the point wont likely be exactly on the line
                // we have backup anyway because some points can be outside all polygons
                float cross = (points[i].Y - point.Y) * (points[i + 1].X - points[i].X) - (points[i].X - point.X) * (points[i + 1].Y - points[i].Y);
                if (cross > 0)
                {
                    left++;
                }
                else
                {
                    right++;
                }
            }
            // same for the last and first point
            float cross2 = (points[i].Y - point.Y) * (points[0].X - points[i].X) - (points[i].X - point.X) * (points[0].Y - points[i].Y);
            if (cross2 > 0)
            {
                left++;
            }
            else
            {
                right++;
            }

            // if all the cross products are positive or negative, the point is inside the polygon
            // points on the line are resolved by the backup
            return left == 0 || right == 0;
        }

        /// <summary>
        /// Gets height of the biome at the given point
        /// Coordinates are cast to float to match the noise function signature
        /// Does not do any chunking, the caller should handle it
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns></returns>
        public int GetHeight(float x, float y)
        {
            noiseWarp.DomainWarp(ref x, ref y);
            return BaseHeight + (int)(HeightAmplitude * noise.GetNoise(x,y));
        }
    }
}
