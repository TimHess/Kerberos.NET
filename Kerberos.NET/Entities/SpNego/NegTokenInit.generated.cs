﻿
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.Asn1;
using Kerberos.NET.Crypto;
using Kerberos.NET.Asn1;

namespace Kerberos.NET.Entities
{
    [StructLayout(LayoutKind.Sequential)]
    public partial struct NegTokenInit
    {
        public Oid[] MechTypes;
    
    public ReadOnlyMemory<byte>? RequestFlags;
        public ReadOnlyMemory<byte>? MechToken;
        public ReadOnlyMemory<byte>? MechListMic;
      
        public ReadOnlySpan<byte> Encode()
        {
            var writer = new AsnWriter(AsnEncodingRules.DER);

            Encode(writer);

            return writer.EncodeAsSpan();
        }
        
        internal void Encode(AsnWriter writer)
        {
            Encode(writer, Asn1Tag.Sequence);
        }
    
        internal void Encode(AsnWriter writer, Asn1Tag tag)
        {
            writer.PushSequence(tag);
            
            writer.PushSequence(new Asn1Tag(TagClass.ContextSpecific, 0));

            writer.PushSequence();
            for (int i = 0; i < MechTypes.Length; i++)
            {
                writer.WriteObjectIdentifier(MechTypes[i]); 
            }
            writer.PopSequence();

            writer.PopSequence(new Asn1Tag(TagClass.ContextSpecific, 0));

            if (RequestFlags.HasValue)
            {
                writer.PushSequence(new Asn1Tag(TagClass.ContextSpecific, 1));
                writer.WriteBitString(RequestFlags.Value.Span);
                writer.PopSequence(new Asn1Tag(TagClass.ContextSpecific, 1));
            }


            if (MechToken.HasValue)
            {
                writer.PushSequence(new Asn1Tag(TagClass.ContextSpecific, 2));
                writer.WriteOctetString(MechToken.Value.Span);
                writer.PopSequence(new Asn1Tag(TagClass.ContextSpecific, 2));
            }


            if (MechListMic.HasValue)
            {
                writer.PushSequence(new Asn1Tag(TagClass.ContextSpecific, 3));
                writer.WriteOctetString(MechListMic.Value.Span);
                writer.PopSequence(new Asn1Tag(TagClass.ContextSpecific, 3));
            }

            writer.PopSequence(tag);
        }
        
        public static NegTokenInit Decode(ReadOnlyMemory<byte> data)
        {
            return Decode(data, AsnEncodingRules.DER);
        }

        internal static NegTokenInit Decode(ReadOnlyMemory<byte> encoded, AsnEncodingRules ruleSet)
        {
            return Decode(Asn1Tag.Sequence, encoded, ruleSet);
        }

        internal static NegTokenInit Decode(Asn1Tag expectedTag, ReadOnlyMemory<byte> encoded)
        {
            AsnReader reader = new AsnReader(encoded, AsnEncodingRules.DER);
            
            Decode(reader, expectedTag, out NegTokenInit decoded);
            reader.ThrowIfNotEmpty();
            return decoded;
        }

        internal static NegTokenInit Decode(Asn1Tag expectedTag, ReadOnlyMemory<byte> encoded, AsnEncodingRules ruleSet)
        {
            AsnReader reader = new AsnReader(encoded, ruleSet);
            
            Decode(reader, expectedTag, out NegTokenInit decoded);
            reader.ThrowIfNotEmpty();
            return decoded;
        }

        internal static void Decode(AsnReader reader, out NegTokenInit decoded)
        {
            if (reader == null)
                throw new ArgumentNullException(nameof(reader));

            Decode(reader, Asn1Tag.Sequence, out decoded);
        }

        internal static void Decode(AsnReader reader, Asn1Tag expectedTag, out NegTokenInit decoded)
        {
            if (reader == null)
                throw new ArgumentNullException(nameof(reader));

            decoded = default;
            AsnReader sequenceReader = reader.ReadSequence(expectedTag);
            AsnReader explicitReader;
            AsnReader collectionReader;
            

            explicitReader = sequenceReader.ReadSequence(new Asn1Tag(TagClass.ContextSpecific, 0));

            // Decode SEQUENCE OF for MechTypes
            {
                collectionReader = explicitReader.ReadSequence();
                var tmpList = new List<Oid>();
                Oid tmpItem;

                while (collectionReader.HasData)
                {
                    tmpItem = collectionReader.ReadObjectIdentifier(); 
                    tmpList.Add(tmpItem);
                }

                decoded.MechTypes = tmpList.ToArray();
            }

            explicitReader.ThrowIfNotEmpty();


            if (sequenceReader.HasData && sequenceReader.PeekTag().HasSameClassAndValue(new Asn1Tag(TagClass.ContextSpecific, 1)))
            {
                explicitReader = sequenceReader.ReadSequence(new Asn1Tag(TagClass.ContextSpecific, 1));

                if (explicitReader.TryReadPrimitiveBitStringValue(out _, out ReadOnlyMemory<byte> tmpRequestFlags))
                {
                    decoded.RequestFlags = tmpRequestFlags;
                }
                else
                {
                    decoded.RequestFlags = explicitReader.ReadBitString(out _);
                }

                explicitReader.ThrowIfNotEmpty();
            }


            if (sequenceReader.HasData && sequenceReader.PeekTag().HasSameClassAndValue(new Asn1Tag(TagClass.ContextSpecific, 2)))
            {
                explicitReader = sequenceReader.ReadSequence(new Asn1Tag(TagClass.ContextSpecific, 2));

                if (explicitReader.TryReadPrimitiveOctetStringBytes(out ReadOnlyMemory<byte> tmpMechToken))
                {
                    decoded.MechToken = tmpMechToken;
                }
                else
                {
                    decoded.MechToken = explicitReader.ReadOctetString();
                }

                explicitReader.ThrowIfNotEmpty();
            }


            if (sequenceReader.HasData && sequenceReader.PeekTag().HasSameClassAndValue(new Asn1Tag(TagClass.ContextSpecific, 3)))
            {
                explicitReader = sequenceReader.ReadSequence(new Asn1Tag(TagClass.ContextSpecific, 3));

                if (explicitReader.TryReadPrimitiveOctetStringBytes(out ReadOnlyMemory<byte> tmpMechListMic))
                {
                    decoded.MechListMic = tmpMechListMic;
                }
                else
                {
                    decoded.MechListMic = explicitReader.ReadOctetString();
                }

                explicitReader.ThrowIfNotEmpty();
            }


            sequenceReader.ThrowIfNotEmpty();
        }
    }
}
