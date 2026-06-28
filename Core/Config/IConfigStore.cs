using System.Threading.Tasks;

namespace TypeMate.Core.Config
{
    public interface IConfigStore
    {
        Task<AppConfig?> GetAsync();
        Task<bool> SaveAsync(AppConfig config);
    }
}
