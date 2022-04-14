using System.Reflection;
using System.Threading.Tasks;

namespace DotLiquid.Util
{
    public static class AsyncMethodInvoker
    {
        public static async Task<object> InvokeAsync(this MethodInfo mi, object obj, params object[] args)
        {
            var isAwaitable = mi.ReturnType.GetMethod(nameof(Task.GetAwaiter)) != null;

            object invokeResult = null;
            if (isAwaitable)
            {
                if (mi.ReturnType.IsGenericType)
                {
                    invokeResult = (object)await(dynamic)mi.Invoke(obj, args);
                }
                else
                {
                    await(Task)mi.Invoke(obj, args);
                }
            }
            else
            {
                if (mi.ReturnType == typeof(void))
                {
                    mi.Invoke(obj, args);
                }
                else
                {
                    invokeResult = mi.Invoke(obj, args);
                }
            }

            return invokeResult;
        }
    }
}
