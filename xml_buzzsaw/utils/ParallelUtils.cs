
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Tasks;

namespace xml_buzzsaw.utils
{
    public static class ParallelUtils
    {
        /// <summary>
        /// Apply an action in parallel over the element of a Concurrent Dictionary
        /// </summary>
        /// <param name="logContext"></param>
        /// <param name="cache"></param>
        /// <param name="action"></param>
        /// <returns></returns>
        public static void ForEach<T1, T2>(ILogger logger, ConcurrentDictionary<T1, T2> cache, Action<T2> action)
        {
            var sw = Stopwatch.StartNew();

            // To help determine if parallelism is overkill, we need to first get the number of proc available.
            //
            int procCount = System.Environment.ProcessorCount;

            // Execute in parallel if there are enough files in the directory
            // Otherwise, just do a plain-jane for loop.
            //
            try
            {
                // if (cache.Count < procCount)
                // {
                    foreach (var item in cache.Values)
                    {
                        action(item);
                    }
                // }
                // else
                // {
                //     Parallel.ForEach(cache.Values, (item) => {
                //         action(item);
                //     });
                // }
            }
            catch (Exception e)
            {
                logger.Error("1839", e.Message + "\n" + e.StackTrace);
            }

        }
    }

}