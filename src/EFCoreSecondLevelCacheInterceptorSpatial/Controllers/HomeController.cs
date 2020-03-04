using EFCoreSecondLevelCacheInterceptor;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite;
using NetTopologySuite.Geometries;
using System.Threading.Tasks;

namespace EFCoreSecondLevelCacheInterceptorSpatial.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _applicationDbContext;

        public HomeController(ApplicationDbContext applicationDbContext)
        {
            _applicationDbContext = applicationDbContext;
        }

        public async Task<IActionResult> Index()
        {
            await _applicationDbContext.Database.EnsureCreatedAsync();

            if (!await _applicationDbContext.Products.AnyAsync())
            {
                _applicationDbContext.People.Add(new Person
                {
                    Name = "Bill"
                });

                var geometryFactory = NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);
                await _applicationDbContext.Products.AddRangeAsync(
                    new Product { Location = geometryFactory.CreatePoint(new Coordinate(27.175015, 78.042155)) },
                    new Product { Location = geometryFactory.CreatePoint(new Coordinate(27.175015, 78.042155)) },
                    new Product { Location = geometryFactory.CreatePoint(new Coordinate(27.175015, 78.042155)) });

                await _applicationDbContext.SaveChangesAsync();
            }

            // Working: Without Spatial
            var peoples = await _applicationDbContext.People.ToListAsync();
            var cachedPeoples = await _applicationDbContext.People.Cacheable().ToListAsync();

            // Working: With Spatial without Caching:
            var products = await _applicationDbContext.Products.ToListAsync();

            // Not Working: With Spatial with Caching:
            var cachedProducts = await _applicationDbContext.Products.Cacheable().ToListAsync();

            return Ok();
        }
    }
}