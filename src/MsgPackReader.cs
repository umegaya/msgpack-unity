//
// Copyright 2011 Kazuki Oikawa
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//   http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//

using System;
using System.IO;
using System.Text;
using System.Collections;

namespace MsgPack
{
	public static class Patch {
		public static long CopyTo(this Stream source, Stream destination) {
			byte[] buffer = new byte[2048];
			int bytesRead;
			long totalBytes = 0;
			while((bytesRead = source.Read(buffer, 0, buffer.Length)) > 0) {
				destination.Write(buffer, 0, bytesRead);
				totalBytes += bytesRead;
			}
			return totalBytes;
		}		
	}
	public class BuffShortException : Exception
	{

	}
	public class MsgPackReader
	{
		Stream _strm;
		MemoryStream _buff;
		Encoding _encoding = Encoding.UTF8;

		byte[] tmp0 = new byte[8];
		byte[] tmp1 = new byte[8];

		public MsgPackReader (Stream strm)
		{
			_strm = strm;
			_buff = new MemoryStream();
		}

		public TypePrefixes Type { get; private set; }
		public sbyte ExtType { get; private set; }
		public uint Length { get; private set; }

		public bool ValueBoolean { get; private set; }
		public uint ValueUnsigned { get; private set; }
		public ulong ValueUnsigned64 { get; private set; }
		public int ValueSigned { get; private set; }
		public long ValueSigned64 { get; private set; }
		public float ValueFloat { get; private set; }
		public double ValueDouble { get; private set; }
		
		public IEnumerator ReadStream(byte[] buf, uint length) {
			return this.ReadStream(buf, (int)length);
		}
		public IEnumerator ReadStream(byte[] buf, int length) {
			while ((this._buff.Length - this._buff.Position) < length) {
				this._strm.CopyTo(this._buff);
				if ((this._buff.Length - this._buff.Position) < length) {
					yield return new BuffShortException();
				}
			}
			this._buff.Read(buf, 0, length);
		}
		public IEnumerator ReadByte(byte[] buf) {
			return this.ReadStream(buf, 1);
		}

		public bool IsSigned ()
		{
			return this.Type == TypePrefixes.NegativeFixNum ||
				this.Type == TypePrefixes.PositiveFixNum ||
				this.Type == TypePrefixes.Int8 ||
				this.Type == TypePrefixes.Int16 ||
				this.Type == TypePrefixes.Int32;
		}
		public bool IsBoolean ()
		{
			return this.Type == TypePrefixes.True || this.Type == TypePrefixes.False;
		}
		public bool IsSigned64 ()
		{
			return this.Type == TypePrefixes.Int64;
		}
		public bool IsUnsigned ()
		{
			return this.Type == TypePrefixes.PositiveFixNum ||
				this.Type == TypePrefixes.UInt8 ||
				this.Type == TypePrefixes.UInt16 ||
				this.Type == TypePrefixes.UInt32;
		}
		public bool IsUnsigned64 ()
		{
			return this.Type == TypePrefixes.UInt64;
		}
		public bool IsStr ()
		{
			return this.Type == TypePrefixes.FixStr || this.Type == TypePrefixes.Str8 || this.Type == TypePrefixes.Str16 || this.Type == TypePrefixes.Str32;
		}
		public bool IsBinary()
		{
			return this.Type == TypePrefixes.Bin8 || this.Type == TypePrefixes.Bin16 || this.Type == TypePrefixes.Bin32;
		}
		public bool IsArray ()
		{
			return this.Type == TypePrefixes.FixArray || this.Type == TypePrefixes.Array16 || this.Type == TypePrefixes.Array32;
		}
		public bool IsMap ()
		{
			return this.Type == TypePrefixes.FixMap || this.Type == TypePrefixes.Map16 || this.Type == TypePrefixes.Map32;
		}

		public string StringifyBytes(byte[] bytes) {
			return this._encoding.GetString(bytes);
		}

		public void Read ()
		{
			this.ReadByte (tmp0);
			int x = tmp0[0];
			
			if (x >= 0x00 && x <= 0x7f) {
				this.Type = TypePrefixes.PositiveFixNum;
			} else if (x >= 0xe0 && x <= 0xff) {
				this.Type = TypePrefixes.NegativeFixNum;
			} else if (x >= 0xa0 && x <= 0xbf) {
				this.Type = TypePrefixes.FixStr;
			} else if (x >= 0x90 && x <= 0x9f) {
				this.Type = TypePrefixes.FixArray;
			} else if (x >= 0x80 && x <= 0x8f) {
				this.Type = TypePrefixes.FixMap;
			} else {
				this.Type = (TypePrefixes)x;
			}

			switch (this.Type) {
				case TypePrefixes.Nil:
					break;
				case TypePrefixes.False:
					ValueBoolean = false;
					break;
				case TypePrefixes.True:
					ValueBoolean = true;
					break;
				case TypePrefixes.Float:
					ValueFloat = ReadSingle ();
					break;
				case TypePrefixes.Double:
					ValueDouble = ReadDouble ();
					break;
				case TypePrefixes.NegativeFixNum:
					ValueSigned = (x & 0x1f) - 0x20;
					break;
				case TypePrefixes.PositiveFixNum:
					ValueSigned = x & 0x7f;
					ValueUnsigned = (uint)ValueSigned;
					break;
				case TypePrefixes.UInt8:
					this.ReadByte (tmp0);
					x = tmp0[0];
					ValueUnsigned = (uint)x;
					break;
				case TypePrefixes.UInt16:
					this.ReadStream (tmp0, 2);
					ValueUnsigned = ((uint)tmp0[0] << 8) | (uint)tmp0[1];
					break;
				case TypePrefixes.UInt32:
					this.ReadStream (tmp0, 4);
					ValueUnsigned = ((uint)tmp0[0] << 24) | ((uint)tmp0[1] << 16) | ((uint)tmp0[2] << 8) | (uint)tmp0[3];
					break;
				case TypePrefixes.UInt64:
					this.ReadStream (tmp0, 8);
					ValueUnsigned64 = ((ulong)tmp0[0] << 56) | ((ulong)tmp0[1] << 48) | ((ulong)tmp0[2] << 40) | ((ulong)tmp0[3] << 32) | ((ulong)tmp0[4] << 24) | ((ulong)tmp0[5] << 16) | ((ulong)tmp0[6] << 8) | (ulong)tmp0[7];
					break;
				case TypePrefixes.Int8:
					this.ReadByte (tmp0);
					x = tmp0[0];
					ValueSigned = (sbyte)x;
					break;
				case TypePrefixes.Int16:
					this.ReadStream (tmp0, 2);
					ValueSigned = (short)((tmp0[0] << 8) | tmp0[1]);
					break;
				case TypePrefixes.Int32:
					this.ReadStream (tmp0, 4);
					ValueSigned = (tmp0[0] << 24) | (tmp0[1] << 16) | (tmp0[2] << 8) | tmp0[3];
					break;
				case TypePrefixes.Int64:
					this.ReadStream (tmp0, 8);
					ValueSigned64 = ((long)tmp0[0] << 56) | ((long)tmp0[1] << 48) | ((long)tmp0[2] << 40) | ((long)tmp0[3] << 32) | ((long)tmp0[4] << 24) | ((long)tmp0[5] << 16) | ((long)tmp0[6] << 8) | (long)tmp0[7];
					break;
				case TypePrefixes.FixStr:
					Length = (uint)(x & 0x1f);
					break;
				case TypePrefixes.FixArray:
				case TypePrefixes.FixMap:
					Length = (uint)(x & 0xf);
					break;
				case TypePrefixes.Str8:
				case TypePrefixes.Bin8:
				case TypePrefixes.Ext8:
					this.ReadStream (tmp0, 1);
					Length = (uint)tmp0 [0];
					break;
				case TypePrefixes.Str16:
				case TypePrefixes.Bin16:
				case TypePrefixes.Array16:
				case TypePrefixes.Map16:
				case TypePrefixes.Ext16:
					this.ReadStream (tmp0, 2);
					Length = ((uint)tmp0[0] << 8) | (uint)tmp0[1];
					break;
				case TypePrefixes.Str32:
				case TypePrefixes.Bin32:
				case TypePrefixes.Array32:
				case TypePrefixes.Map32:
				case TypePrefixes.Ext32:
					this.ReadStream (tmp0, 4);
					Length = ((uint)tmp0[0] << 24) | ((uint)tmp0[1] << 16) | ((uint)tmp0[2] << 8) | (uint)tmp0[3];
					break;
				case TypePrefixes.FixExt8:
					Length = 1;
					break;
				case TypePrefixes.FixExt16:
					Length = 2;
					break;
				case TypePrefixes.FixExt32:
					Length = 4;
					break;
				case TypePrefixes.FixExt64:
					Length = 8;
					break;
				case TypePrefixes.FixExt128:
					Length = 16;
					break;
				default:
					throw new FormatException ();
			}
		}

		public IEnumerator ReadExtType (byte[] buf)
		{
			return this.ReadByte(buf);
		}

		public IEnumerator ReadRawBytes (byte[] buf)
		{
			return this.ReadStream(buf, this.Length);
		}

		public float ReadSingle() {
			this.ReadStream (tmp0, 4);
			if (BitConverter.IsLittleEndian) {
				tmp1 [0] = tmp0 [3];
				tmp1 [1] = tmp0 [2];
				tmp1 [2] = tmp0 [1];
				tmp1 [3] = tmp0 [0];
				return BitConverter.ToSingle (tmp1, 0);
			} else {
				return BitConverter.ToSingle (tmp0, 0);
			}
		}

		public double ReadDouble() {
			this.ReadStream (tmp0, 8);
			if (BitConverter.IsLittleEndian) {
				tmp1[0] = tmp0[7];
				tmp1[1] = tmp0[6];
				tmp1[2] = tmp0[5];
				tmp1[3] = tmp0[4];
				tmp1[4] = tmp0[3];
				tmp1[5] = tmp0[2];
				tmp1[6] = tmp0[1];
				tmp1[7] = tmp0[0];
				return BitConverter.ToDouble (tmp1, 0);
			} else {
				return BitConverter.ToDouble (tmp0, 0);
			}
		}
	}
}
