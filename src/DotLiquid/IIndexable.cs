using System.Threading.Tasks;

namespace DotLiquid
{
    public interface IIndexable
    {
        Task<object> GetAsync(object key);
        bool ContainsKey(object key);
    }
}
