using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace NetInterface
{
    public static class AssemblyTypeLoaderExtensions
    {
        /// <summary>
        /// Add a single service of type T from an external assembly
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="services"></param>
        /// <param name="path"></param>
        /// <returns></returns>
        public static IServiceCollection AddFromAssembly<T>(this IServiceCollection services, string path)
        {
            Type itype = typeof(T);
            // load assembly

            try
            {
                if (File.Exists(path))
                {
                    Assembly assembly = Assembly.LoadFrom(path);
                    // get types implementing this interface
                    Type[] types = assembly.GetTypes().Where(x => itype.IsAssignableFrom(x)).ToArray();
                    if (types.Length == 0)
                        throw new NotImplementedException($"The referenced assembly does not implement interface {itype.Name}");
                    if (types.Length > 1)
                        throw new TypeLoadException($"The referenced assembly has multiple implementations of interface {itype.Name}");
                    // add to service collection as singleton (one instance for all requests)
                    services.TryAddSingleton(itype, types[0]);
                    // other options: scoped (one created for every request; same lifetime as controller) or transient (created every time)

                    Console.WriteLine($"INFO: Service {itype.Name} added from assembly: {path}");
                }
                else
                {
                    Console.WriteLine($"ERROR: Assembly file not found: {path}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR: Could not load assembly from {path}. error: {ex}");
            }


            // possible exceptions:
            //  file not found? (exception from LoadFrom)
            //  multiple or no match for interface
            //  ??
            return services;
        }

        /*public static IServiceCollection AddFromAssemblies<T>(this IServiceCollection services, string path, string pattern)
		{
			Type itype = typeof(T);
			foreach (string file in System.IO.Directory.GetFiles(path, pattern))
			{
				Assembly assembly;
				try
				{
					// load assembly
					assembly = Assembly.LoadFrom(file);
				}
				catch (Exception)
				{
					// ignore exceptions from loading
					continue;
				}
				// get types implementing this interface
				Type[] types = assembly.GetTypes().Where(x => itype.IsAssignableFrom(x)).ToArray();
				if (types.Length == 0)
					throw new NotImplementedException($"The referenced assembly does not implement interface {itype.Name}");
				// add to service collection as singleton (one instance for all requests)
				foreach (Type type in types)
					services.TryAddSingleton(itype, type);
				// other options: scoped (one created for every request; same lifetime as controller) or transient (created every time)
			}

			// possible exceptions:
			//  file not found? (exception from LoadFrom)
			//  multiple or no match for interface
			//  ??
			return services;
		}*/
    }
}
