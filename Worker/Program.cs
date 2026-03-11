using System.Security.Cryptography;
using System.Text;

namespace Program;
class Program
{
    private static readonly char[] alphabet = "abcdefghijklmnopqrstuvwxyz0123456789".ToCharArray();
    private static IEnumerable<string> GetAllMatches(int length)
    {
        var indexes = new int[length];
        var current = new char[length];
        
        for (var i=0; i < length; i++)
            current[i] = alphabet[0];
        do
        {
            yield return new string(current);
        }
        while (Increment(indexes, current));
    }

    private static bool Increment(int[] indexes, char[] current)
    {
        var position = indexes.Length-1;

        while (position >= 0)
        {
            indexes[position]++;
            if (indexes[position] < alphabet.Length)
            {
                current[position] = alphabet[indexes[position]];
                return true;
            }
            indexes[position] = 0;
            current[position] = alphabet[0];
            position--;
        }
        return false;
    }

    private static string ComputeMd5Hash(string s)
    {
        using (var md5Hash = MD5.Create())
        {
            var data = md5Hash.ComputeHash(Encoding.UTF8.GetBytes(s));
            var sBuilder = new StringBuilder();
            
            foreach (var b in data)
                sBuilder.Append(b.ToString("x2"));
            
            return sBuilder.ToString();
        }
    }

    private static List<string> MakeBruteForce(string targetHash, int maxLength)
    {
        var results = new List<string>();
        
        foreach (var candidate in GetAllMatches(maxLength))
        {
            if (ComputeMd5Hash(candidate) == targetHash)
                results.Add(candidate);
        }
        
        return results;
    }
    
    static void Main(string[] args)
    {
        var inputHash = Console.ReadLine();
        var maxLength = Int32.Parse(Console.ReadLine()!);
        
        foreach (var res in MakeBruteForce(inputHash, maxLength))
            Console.WriteLine(res);
    }
}