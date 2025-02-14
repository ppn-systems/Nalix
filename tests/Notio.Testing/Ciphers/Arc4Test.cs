//using Notio.Cryptography.Ciphers.Symmetric;
//using System;
//using Xunit;

//namespace Notio.Testing.Ciphers
//{
//    public class Arc4Tests
//    {
//        [Fact]
//        public void Encrypt_Decrypt_ShouldReturnOriginalData()
//        {
//            // Arrange
//            byte[] key = { 1, 2, 3, 4, 5 };
//            byte[] plaintext = { 72, 101, 108, 108, 111 }; // "Hello"
//            byte[] encrypted = new byte[plaintext.Length];
//            byte[] decrypted = new byte[plaintext.Length];

//            plaintext.CopyTo(encrypted, 0);

//            // Dùng một instance để mã hóa
//            var arc4Encryptor = new Arc4(key);
//            arc4Encryptor.Process(encrypted);

//            encrypted.CopyTo(decrypted, 0);

//            // Dùng một instance khác để giải mã
//            var arc4Decryptor = new Arc4(key);
//            arc4Decryptor.Process(decrypted);

//            // Assert
//            Assert.Equal(plaintext, decrypted);
//        }

//        [Fact]
//        public void DifferentKeys_ShouldProduceDifferentResults()
//        {
//            // Arrange
//            byte[] key1 = { 1, 2, 3, 4, 5 };
//            byte[] key2 = { 6, 7, 8, 9, 10 };
//            byte[] plaintext = { 72, 101, 108, 108, 111 }; // "Hello"
//            byte[] encryptedWithKey1 = new byte[plaintext.Length];
//            byte[] encryptedWithKey2 = new byte[plaintext.Length];

//            plaintext.CopyTo(encryptedWithKey1, 0);
//            plaintext.CopyTo(encryptedWithKey2, 0);

//            var arc4Encryptor1 = new Arc4(key1);
//            arc4Encryptor1.Process(encryptedWithKey1);

//            var arc4Encryptor2 = new Arc4(key2);
//            arc4Encryptor2.Process(encryptedWithKey2);

//            // Assert
//            Assert.NotEqual(encryptedWithKey1, encryptedWithKey2);
//        }

//        [Fact]
//        public void EmptyInput_ShouldRemainEmpty()
//        {
//            // Arrange
//            byte[] key = { 1, 2, 3, 4, 5 };
//            byte[] emptyData = Array.Empty<byte>();

//            var arc4 = new Arc4(key);
//            arc4.Process(emptyData);

//            // Assert
//            Assert.Empty(emptyData);
//        }

//        [Fact]
//        public void InvalidKey_ShouldThrowArgumentException()
//        {
//            // Arrange
//            byte[] shortKey = { 1, 2, 3, 4 };  // Too short
//            byte[] longKey = new byte[257]; // Too long

//            // Act & Assert
//            Assert.Throws<ArgumentException>(() => new Arc4(shortKey));
//            Assert.Throws<ArgumentException>(() => new Arc4(longKey));
//        }

//        [Fact]
//        public void NullKey_ShouldThrowArgumentException()
//        {
//            // Act & Assert
//            Assert.Throws<ArgumentException>(() => new Arc4(null));
//        }
//    }
//}
