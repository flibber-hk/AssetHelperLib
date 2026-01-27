using System.IO;

namespace AssetHelperLib.IO;

internal static class Extensions
{
    public static int ReadExact(this FileStream fs, byte[] array, int offset, int count)
    {
        int totalBytesRead = 0;

        while (totalBytesRead < count)
        {
            int bytesRead = fs.Read(array, offset + totalBytesRead, count - totalBytesRead);

            if (bytesRead == 0)
            {
                return totalBytesRead;
            }

            totalBytesRead += bytesRead;
        }

        return totalBytesRead;
    }
}
