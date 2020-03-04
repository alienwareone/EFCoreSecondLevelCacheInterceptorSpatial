using NetTopologySuite.Geometries;

namespace EFCoreSecondLevelCacheInterceptorSpatial
{
    public class Product
    {
        public int Id { get; set; }
        public Point Location { get; set; }
    }
}