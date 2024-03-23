using Newtonsoft.Json;
using NoiseLibrary;

namespace TerrainGenerationPCG
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Utils.CreateBiomeMap();
            //Utils.CreateConfig("config.json");
        }
    }
}
