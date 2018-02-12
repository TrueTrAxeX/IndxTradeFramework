using System;
using System.Security.Cryptography;
using System.Text;

namespace IndxTradeFramework.TradeApi
{
    public class Crypto
    {
        public static byte[] Hash(string plainString, Encoding encoding)
        {
            if (plainString == null)
                throw new ArgumentNullException("plainString");

            if (encoding == null)
                encoding = Encoding.UTF8;

            return Hash(encoding.GetBytes(plainString));
        }

        public static byte[] Hash(byte[] bytes)
        {
            if (bytes == null)
                throw new ArgumentNullException("bytes");
            using (SHA256 algorithm = new SHA256Managed())
            {
                byte[] hashBytes = algorithm.ComputeHash(bytes);
                return hashBytes;
            }
        }

        public static string HashToBase64(string plainString, Encoding encoding)
        {
            if (plainString == null) throw new ArgumentNullException("plainString");
            return Convert.ToBase64String(Hash(plainString, encoding));
        }
    }
}