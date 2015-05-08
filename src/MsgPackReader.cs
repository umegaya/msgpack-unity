//
// Copyright 2015 Takehiro Iyatomi
// based on the code which Copyright 2011 Kazuki Oikawa, but heavily modified for supporting streaming unpack.
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
using UnityEngine;

namespace MsgPack
{
	public class BuffShortException : Exception {}
	public static class Patch {
		public static int WSAEWOULDBLOCK = 10035;
		public static bool SocketClosed(this IOException source) {
			//get IOException.InnerException
			var e = source.InnerException;
			if (e is System.Net.Sockets.SocketException) {
				var se = (System.Net.Sockets.SocketException)e;
				//WSAEWOULDBLOCK seems like EAGAIN or EPROGRESS for non-blocking socket on linux
				return se.ErrorCode != WSAEWOULDBLOCK;
			}
			return false;
		}
		public static long CopyTo(this Stream source, Stream destination) {
			byte[] buffer = new byte[2048];
			int bytesRead;
			long totalBytes = 0;
			try {
				while (((bytesRead = source.Read(buffer, 0, buffer.Length)) > 0)) {
					destination.Write(buffer, 0, bytesRead);
					totalBytes += bytesRead;
				}
			}
			catch (IOException e) {
				if (e.SocketClosed()) {
					throw e;
				}
			}
			return totalBytes;
		}
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
				yield return new BuffShortException();
				var oldpos = this._buff.Position;
				if (this._strm.CopyTo(this._buff) > 0) {
					this._buff.Seek(oldpos, SeekOrigin.Begin);
				}
				//Debug.Log("readst2:" + this._buff.Length + "|" + this._buff.Position + "|" + length + "|" + this._strm.CanRead);
			}
			this._buff.Read(buf, 0, length);
			if (this._buff.Position == this._buff.Length) {
				this._buff.SetLength(0);
			}
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

		//TODO : need to rewrite as finite state machine for faster decoding
		public IEnumerator Read ()
		{
			IEnumerator it;
			it = this.ReadStream(tmp0, 1); while (it.MoveNext()) { yield return it.Current; };
			int x = tmp0[0];
			//Debug.Log("Read:x:"+x);
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
					it = this.ReadStream (tmp0, 4); while (it.MoveNext()) { yield return it.Current; };
					ValueFloat = ReadSingle (tmp0);
					break;
				case TypePrefixes.Double:
					it = this.ReadStream (tmp0, 8); while (it.MoveNext()) { yield return it.Current; };
					ValueDouble = ReadDouble (tmp0);
					break;
				case TypePrefixes.NegativeFixNum:
					ValueSigned = (x & 0x1f) - 0x20;
					break;
				case TypePrefixes.PositiveFixNum:
					ValueSigned = x & 0x7f;
					ValueUnsigned = (uint)ValueSigned;
					break;
				case TypePrefixes.UInt8:
					it = this.ReadStream (tmp0, 1); while (it.MoveNext()) { yield return it.Current; };
					x = tmp0[0];
					ValueUnsigned = (uint)x;
					break;
				case TypePrefixes.UInt16:
					it = this.ReadStream (tmp0, 2); while (it.MoveNext()) { yield return it.Current; };
					ValueUnsigned = ((uint)tmp0[0] << 8) | (uint)tmp0[1];
					break;
				case TypePrefixes.UInt32:
					it = this.ReadStream (tmp0, 4); while (it.MoveNext()) { yield return it.Current; };
					ValueUnsigned = ((uint)tmp0[0] << 24) | ((uint)tmp0[1] << 16) | ((uint)tmp0[2] << 8) | (uint)tmp0[3];
					break;
				case TypePrefixes.UInt64:
					it = this.ReadStream (tmp0, 8); while (it.MoveNext()) { yield return it.Current; };
					ValueUnsigned64 = ((ulong)tmp0[0] << 56) | ((ulong)tmp0[1] << 48) | ((ulong)tmp0[2] << 40) | ((ulong)tmp0[3] << 32) | ((ulong)tmp0[4] << 24) | ((ulong)tmp0[5] << 16) | ((ulong)tmp0[6] << 8) | (ulong)tmp0[7];
					break;
				case TypePrefixes.Int8:
					it = this.ReadStream (tmp0, 1); while (it.MoveNext()) { yield return it.Current; };
					x = tmp0[0];
					ValueSigned = (sbyte)x;
					break;
				case TypePrefixes.Int16:
					it = this.ReadStream (tmp0, 2); while (it.MoveNext()) { yield return it.Current; };
					ValueSigned = (short)((tmp0[0] << 8) | tmp0[1]);
					break;
				case TypePrefixes.Int32:
					it = this.ReadStream (tmp0, 4); while (it.MoveNext()) { yield return it.Current; };
					ValueSigned = (tmp0[0] << 24) | (tmp0[1] << 16) | (tmp0[2] << 8) | tmp0[3];
					break;
				case TypePrefixes.Int64:
					it = this.ReadStream (tmp0, 8); while (it.MoveNext()) { yield return it.Current; };
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
					it = this.ReadStream (tmp0, 1); while (it.MoveNext()) { yield return it.Current; };
					Length = (uint)tmp0 [0];
					break;
				case TypePrefixes.Str16:
				case TypePrefixes.Bin16:
				case TypePrefixes.Array16:
				case TypePrefixes.Map16:
				case TypePrefixes.Ext16:
					it = this.ReadStream (tmp0, 2); while (it.MoveNext()) { yield return it.Current; };
					Length = ((uint)tmp0[0] << 8) | (uint)tmp0[1];
					break;
				case TypePrefixes.Str32:
				case TypePrefixes.Bin32:
				case TypePrefixes.Array32:
				case TypePrefixes.Map32:
				case TypePrefixes.Ext32:
					it = this.ReadStream (tmp0, 4); while (it.MoveNext()) { yield return it.Current; };
					Length = ((uint)tmp0[0] << 24) | ((uint)tmp0[1] << 16) | ((uint)tmp0[2] << 8) | (uint)tmp0[3];
					break;
				case TypePrefixes.FixExt8:
					Type = TypePrefixes.Ext8;
					Length = 1;
					break;
				case TypePrefixes.FixExt16:
					Type = TypePrefixes.Ext8;
					Length = 2;
					break;
				case TypePrefixes.FixExt32:
					Type = TypePrefixes.Ext8;
					Length = 4;
					break;
				case TypePrefixes.FixExt64:
					Type = TypePrefixes.Ext8;
					Length = 8;
					break;
				case TypePrefixes.FixExt128:
					Type = TypePrefixes.Ext8;
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

		public float ReadSingle(byte[] buf) {
			if (BitConverter.IsLittleEndian) {
				tmp1 [0] = buf [3];
				tmp1 [1] = buf [2];
				tmp1 [2] = buf [1];
				tmp1 [3] = buf [0];
				return BitConverter.ToSingle (tmp1, 0);
			} else {
				return BitConverter.ToSingle (buf, 0);
			}
		}

		public double ReadDouble(byte[] buf) {
			if (BitConverter.IsLittleEndian) {
				tmp1[0] = buf[7];
				tmp1[1] = buf[6];
				tmp1[2] = buf[5];
				tmp1[3] = buf[4];
				tmp1[4] = buf[3];
				tmp1[5] = buf[2];
				tmp1[6] = buf[1];
				tmp1[7] = buf[0];
				return BitConverter.ToDouble (tmp1, 0);
			} else {
				return BitConverter.ToDouble (buf, 0);
			}
		}
	}
}
