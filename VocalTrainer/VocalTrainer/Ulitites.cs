using System;

namespace VocalTrainer
{
    internal static class Ulitites
    {
        public static T ConvertToEnum<T>(this string enumString)
        {
            try
            {
                return (T)Enum.Parse(typeof(T), enumString, true);
            }
            catch (Exception ex)
            {
               throw new Exception("Can't ConvertToEnum!", ex);
            }
        }
    }
}
