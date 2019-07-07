﻿using Kerberos.NET.Crypto;
using System;

namespace KerbDump
{
    public class NoopTransform : KerberosCryptoTransformer
    {
        public override int ChecksumSize => throw new NotImplementedException();

        public override int BlockSize => throw new NotImplementedException();

        public override int KeySize => throw new NotImplementedException();

        public override ReadOnlyMemory<byte> Encrypt(ReadOnlySpan<byte> data, KerberosKey key, KeyUsage usage)
        {
            return new ReadOnlyMemory<byte>(data.ToArray());
        }

        public override byte[] Decrypt(ReadOnlyMemory<byte> cipher, KerberosKey key, KeyUsage usage)
        {
            return cipher.ToArray();
        }

        public override byte[] String2Key(KerberosKey key)
        {
            return key.PasswordBytes;
        }
    }
}