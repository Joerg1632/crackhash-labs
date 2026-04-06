using System.Security.Cryptography;
using System.Text;

namespace Worker.Utils;

public class HashingHelper
{
    
    public static string ComputeMd5Hash(string s)
    {
        using var md5 = MD5.Create();
        var data = md5.ComputeHash(Encoding.UTF8.GetBytes(s));
        var sb = new StringBuilder();
        foreach (var b in data)
            sb.Append(b.ToString("x2"));
        return sb.ToString();
    }

    public static string IndexToString(long index, int maxLength, char[] alphabet)
    {
        var baseN = alphabet.Length;
        var length = 1;
        var count = baseN;

        while (index >= count && length < maxLength)
        {
            index -= count;
            length++;
            count *= baseN;
        }

        var chars = new char[length];
        for (var i = length - 1; i >= 0; i--)
        {
            chars[i] = alphabet[(int)(index % baseN)];
            index /= baseN;
        }

        return new string(chars);
    }
}