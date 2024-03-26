using Newtonsoft.Json;
using NoiseLibrary;

namespace TerrainGenerationPCG
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            MapGenerator mg = new MapGenerator();
            mg.GenerateHeightMap(800, 800, 1);
            watch.Stop();
            mg.WhittakerDiagram.PrintBiomeConfigs();
            Console.WriteLine();
            Console.WriteLine($"Execution Time: {watch.ElapsedMilliseconds} ms");
            
        }
    }
}
