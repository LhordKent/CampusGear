using AutoMapper;
using CampusGear.Data;
using CampusGear.Data.Interfaces;
using CampusGear.Data.Repositories;
using CampusGear.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace CampusGear.IntegrationTests
{
    [Collection("Web app permissions")]
    public sealed class BasecodeConformityTests(WebAppFixture app)
    {
        [Fact]
        public void Every_application_repository_uses_the_shared_base_and_resolves_its_public_contract()
        {
            var repositories = typeof(ApplicationDbContext)
                .Assembly.GetTypes()
                .Where(type => type.IsClass && !type.IsAbstract && !type.IsNested && type.Namespace == "CampusGear.Data.Repositories")
                .ToArray();
            Assert.True(repositories.Length >= 14);
            using var scope = app.Services.CreateScope();
            foreach (var repository in repositories)
            {
                Assert.True(repository.IsPublic);
                Assert.True(typeof(BaseRepository).IsAssignableFrom(repository), repository.FullName);
                var contract = Assert.Single(repository.GetInterfaces().Where(type => type.Name == "I" + repository.Name));
                Assert.True(contract.IsPublic);
                var instance = scope.ServiceProvider.GetRequiredService(contract);
                Assert.Equal(repository, instance.GetType());
                Assert.Same(instance, scope.ServiceProvider.GetRequiredService(contract));
            }

            Assert.Same(scope.ServiceProvider.GetRequiredService<IUnitOfWork>(), scope.ServiceProvider.GetRequiredService<IUnitOfWork>());
        }

        [Fact]
        public void Every_business_controller_inherits_its_own_generic_controller_base()
        {
            var exceptions = new[] { "DevelopmentController", "StatusController" };
            var controllers = typeof(Program)
                .Assembly.GetTypes()
                .Where(type => !type.IsAbstract && typeof(Controller).IsAssignableFrom(type) && !exceptions.Contains(type.Name))
                .ToArray();
            Assert.True(controllers.Length >= 11);
            foreach (var controller in controllers)
            {
                var expected = typeof(CampusGear.WebApp.Mvc.ControllerBase<>).MakeGenericType(controller);
                Assert.True(expected.IsAssignableFrom(controller), controller.FullName);
            }
        }

        [Fact]
        public void Every_business_service_contract_resolves_a_matching_scoped_implementation()
        {
            var contracts = typeof(ICategoryService)
                .Assembly.GetTypes()
                .Where(type => type.IsInterface && type.Namespace == "CampusGear.Services.Interfaces" && type.Name.EndsWith("Service"))
                .ToArray();
            Assert.True(contracts.Length >= 18);
            using var scope = app.Services.CreateScope();
            using var otherScope = app.Services.CreateScope();
            foreach (var contract in contracts)
            {
                var instance = scope.ServiceProvider.GetRequiredService(contract);
                Assert.Equal(contract.Name[1..], instance.GetType().Name);
                Assert.Same(instance, scope.ServiceProvider.GetRequiredService(contract));
                Assert.NotSame(instance, otherScope.ServiceProvider.GetRequiredService(contract));
            }
        }

        [Fact]
        public void Mapper_configuration_only_contains_valid_read_model_mappings()
        {
            app.Services.GetRequiredService<IMapper>().ConfigurationProvider.AssertConfigurationIsValid();
        }
    }
}
