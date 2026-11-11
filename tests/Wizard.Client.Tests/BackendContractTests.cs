using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Wizard.Client;
using Wizard.Core;
using Xunit;

namespace Wizard.Client.Tests
{
    // CONTRACTS 0.2·6.1, T-26: 클라이언트의 모든 비동기 호출은 Task<Result<T>>, 오프라인과 서버는 IBackend/IOnlineBackend 로 가른다.
    public sealed class BackendContractTests
    {
        [Fact]
        public void OnlineBackendExtendsBackend()
        {
            Assert.True(typeof(IBackend).IsAssignableFrom(typeof(IOnlineBackend)));
            Assert.True(typeof(IDisposable).IsAssignableFrom(typeof(IBackend)));
        }

        [Fact]
        public void EveryAsyncServiceMethodReturnsTaskOfResult()
        {
            Type[] services = typeof(IBackend).Assembly.GetTypes()
                .Where(t => t.IsInterface && t.Namespace == "Wizard.Client" && (t.Name.EndsWith("Service") || t.Name == "IAuthClient" || t.Name == "IBackendFactory"))
                .ToArray();
            Assert.True(services.Length >= 20, "expected all service interfaces, found " + services.Length);
            foreach (Type s in services)
            {
                foreach (MethodInfo m in s.GetMethods().Where(m => m.Name.EndsWith("Async")))
                {
                    Type r = m.ReturnType;
                    Assert.True(r.IsGenericType && r.GetGenericTypeDefinition() == typeof(Task<>), $"{s.Name}.{m.Name} must return Task<…>");
                    Type inner = r.GetGenericArguments()[0];
                    Assert.True(inner.IsGenericType && inner.GetGenericTypeDefinition() == typeof(Result<>), $"{s.Name}.{m.Name} must return Task<Result<T>>");
                }
            }
        }

        [Fact]
        public void ClientDoesNotReferenceServerProjects()
        {
            Assert.DoesNotContain(typeof(IBackend).Assembly.GetReferencedAssemblies(), a => a.Name!.StartsWith("Wizard.Server."));
        }
    }
}
