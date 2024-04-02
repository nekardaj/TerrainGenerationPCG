using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using NoiseLibrary;
using UnityEngine;
using Vector2 = System.Numerics.Vector2;

namespace TerrainGenerationPCG
{



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
            string path = Application.dataPath + "\\Scripts\\TerrainGeneration\\TerrainGenerationPCG";

            TemperatureConfig =
                JsonConvert.DeserializeObject<NoiseConfig>(
                    System.IO.File.ReadAllText(path + "\\TemperatureConfig.json"));
            PrecipationConfig =
                JsonConvert.DeserializeObject<NoiseConfig>(
                    System.IO.File.ReadAllText(path + "\\PrecipationConfig.json"));
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


        private const float displacementStep = 7;

        private static List<Vector2> filteringDisplacements = new List<Vector2>()
        {
            displacementStep * new Vector2(1, 0), displacementStep * new Vector2(-1, 0),
            displacementStep * new Vector2(0, 1), displacementStep * new Vector2(0, -1),
            displacementStep * new Vector2(0.5f, 0.5f), displacementStep * new Vector2(-0.5f, 0.5f),
            displacementStep * new Vector2(0.5f, -0.5f), displacementStep * new Vector2(-0.5f, -0.5f)
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
            temperatureNoiseWarp.DomainWarp(ref xWarpTemp, ref yWarpTemp);
            float xWarpPrec = x;
            float yWarpPrec = y;
            precipationNoiseWarp.DomainWarp(ref xWarpPrec, ref yWarpPrec);

            // points used in filtering vote for the biome type
            int[] BiomeTypeVotes = new int[(int)BiomeType.Count];

            var biome = WhittakerDiagram.GetBiome((temperatureNoise.GetNoise(xWarpTemp, yWarpTemp) + 1) / 2,
                (precipationNoise.GetNoise(xWarpPrec, yWarpPrec) + 1) / 2);
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
}