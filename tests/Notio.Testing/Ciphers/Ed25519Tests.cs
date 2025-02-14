//using Notio.Cryptography.Ciphers.Asymmetric;
//using System;
//using System.Linq;
//using Xunit;

//namespace Notio.Testing.Ciphers
//{
//    public class Ed25519Tests
//    {
//        // Test for signing and verifying a message
//        [Fact]
//        public void TestSignAndVerify_Success()
//        {
//            // Generate a private key (32 bytes random data for testing)
//            byte[] privateKey = new byte[32];
//            new Random().NextBytes(privateKey);

//            // Create a test message
//            byte[] message = [1, 2, 3, 4, 5, 6];

//            // Sign the message
//            byte[] signature = Ed25519.Sign(message, privateKey);

//            // Derive public key from the private key
//            byte[] publicKey = new byte[32];
//            Ed25519.ComputeHash(privateKey).AsSpan(0, 32).CopyTo(publicKey);

//            // Verify the signature
//            bool isValid = Ed25519.Verify(signature, message, publicKey);

//            // Assert the signature is valid
//            Assert.True(isValid);
//        }

//        // Test for signature verification failure with an altered message
//        [Fact]
//        public void TestSignAndVerify_Failure_InvalidMessage()
//        {
//            // Generate a private key
//            byte[] privateKey = new byte[32];
//            new Random().NextBytes(privateKey);

//            // Create a test message
//            byte[] message = [1, 2, 3, 4, 5, 6];

//            // Sign the message
//            byte[] signature = Ed25519.Sign(message, privateKey);

//            // Alter the message to simulate tampering
//            byte[] alteredMessage = [7, 8, 9, 10, 11, 12];

//            // Derive public key from the private key
//            byte[] publicKey = new byte[32];
//            Ed25519.ComputeHash(privateKey).AsSpan(0, 32).CopyTo(publicKey);

//            // Verify the signature against the altered message
//            bool isValid = Ed25519.Verify(signature, alteredMessage, publicKey);

//            // Assert the signature is invalid
//            Assert.False(isValid);
//        }

//        // Test for signature verification failure with an invalid signature
//        [Fact]
//        public void TestSignAndVerify_Failure_InvalidSignature()
//        {
//            // Generate a private key
//            byte[] privateKey = new byte[32];
//            new Random().NextBytes(privateKey);

//            // Create a test message
//            byte[] message = [1, 2, 3, 4, 5, 6];

//            // Sign the message
//            byte[] signature = Ed25519.Sign(message, privateKey);

//            // Alter the signature to simulate tampering
//            byte[] tamperedSignature = new byte[64];
//            signature.CopyTo(tamperedSignature, 0);
//            tamperedSignature[0] = (byte)(tamperedSignature[0] ^ 0x01); // Flip the first bit

//            // Derive public key from the private key
//            byte[] publicKey = new byte[32];
//            Ed25519.ComputeHash(privateKey).AsSpan(0, 32).CopyTo(publicKey);

//            // Verify the tampered signature
//            bool isValid = Ed25519.Verify(tamperedSignature, message, publicKey);

//            // Assert the signature is invalid
//            Assert.False(isValid);
//        }

//        // Test for signature generation with the same private key and different messages
//        [Fact]
//        public void TestSignWithSamePrivateKey_DifferentMessages()
//        {
//            // Generate a private key
//            byte[] privateKey = new byte[32];
//            new Random().NextBytes(privateKey);

//            // Create two different messages
//            byte[] message1 = [1, 2, 3, 4, 5];
//            byte[] message2 = [6, 7, 8, 9, 10];

//            // Sign both messages
//            byte[] signature1 = Ed25519.Sign(message1, privateKey);
//            byte[] signature2 = Ed25519.Sign(message2, privateKey);

//            // Assert that the signatures for different messages are different
//            Assert.False(signature1.SequenceEqual(signature2));
//        }
//    }
//}
