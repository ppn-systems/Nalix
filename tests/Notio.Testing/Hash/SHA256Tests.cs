using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace Notio.Testing.Hash
{
    public class Sha256Tests
    {
        [Fact]
        public void Hash_EmptyData_ShouldMatchKnownSHA256()
        {
            // Arrange
            string input = "";
            string expectedHash = "E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855";

            // Act
            string actualHash = Notio.Cryptography.Hash.Sha256.Result(input);

            // Assert
            Assert.Equal(expectedHash, actualHash, ignoreCase: true);
        }

        [Fact]
        public void Hash_KnownData_ShouldMatchSHA256Standard()
        {
            // Arrange
            string input = "hello world";
            string expectedHash = "B94D27B9934D3E08A52E52D7DA7DABFAE73DBEA07E66333482A2391C15BFADE1";

            // Act
            string actualHash = Notio.Cryptography.Hash.Sha256.Result(input);

            // Assert
            Assert.Equal(expectedHash, actualHash, ignoreCase: true);
        }

        [Fact]
        public void Hash_LargeData_ShouldBeCorrect()
        {
            // Arrange
            byte[] largeData = new byte[1_000_000]; // 1MB dữ liệu rỗng
            string expectedHash;
            using (SHA256 sha256 = SHA256.Create())
            {
                expectedHash = BitConverter.ToString(sha256.ComputeHash(largeData)).Replace("-", "");
            }

            // Act
            string actualHash = Notio.Cryptography.Hash.Sha256.Result(Encoding.UTF8.GetString(largeData));

            // Assert
            Assert.Equal(expectedHash, actualHash, ignoreCase: true);
        }

        [Fact]
        public void Hash_VariableLengthData_ShouldBeConsistent()
        {
            for (int size = 1; size <= 1024; size *= 2)
            {
                // Arrange
                byte[] data = Enumerable.Repeat((byte)0xAB, size).ToArray();
                string expectedHash;
                using (SHA256 sha256 = SHA256.Create())
                {
                    expectedHash = BitConverter.ToString(sha256.ComputeHash(data)).Replace("-", "");
                }

                // Act
                string actualHash = Notio.Cryptography.Hash.Sha256.Result(Encoding.UTF8.GetString(data));

                // Assert
                Assert.Equal(expectedHash, actualHash, ignoreCase: true);
            }
        }

        [Fact]
        public void Hash_MultipleUpdates_ShouldBeConsistent()
        {
            // Arrange
            byte[] part1 = Encoding.UTF8.GetBytes("hello ");
            byte[] part2 = Encoding.UTF8.GetBytes("world");

            string expectedHash;
            using (SHA256 sha256 = SHA256.Create())
            {
                expectedHash = BitConverter.ToString(sha256.ComputeHash(part1.Concat(part2).ToArray())).Replace("-", "");
            }

            // Act
            string actualHash = Notio.Cryptography.Hash.Sha256.Result("hello world");

            // Assert
            Assert.Equal(expectedHash, actualHash, ignoreCase: true);
        }

        [Fact]
        public void Hash_CompareWithDotNetImplementation()
        {
            // Arrange
            string input = "benchmark test";
            string expectedHash;
            using (SHA256 sha256 = SHA256.Create())
            {
                expectedHash = BitConverter.ToString(sha256.ComputeHash(Encoding.UTF8.GetBytes(input))).Replace("-", "");
            }

            // Act
            string actualHash = Notio.Cryptography.Hash.Sha256.Result(input);

            // Assert
            Assert.Equal(expectedHash, actualHash, ignoreCase: true);
        }
    }
}
