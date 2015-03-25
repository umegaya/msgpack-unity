//
// Copyright 2011 Kazuki Oikawa, Kazunari Kida
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


namespace MsgPack
{
	public class MsgPackWriter
	{
		Stream _strm;
		//Encoding _encoding = Encoding.UTF8;
		Encoder _encoder = Encoding.UTF8.GetEncoder ();
		byte[] _tmp = new byte[9];
		byte[] _buf = new byte[64];

		public MsgPackWriter (Stream strm)
		{
			_strm = strm;
		}

		public void Write (byte x)
		{
			byte[] tmp = _tmp;
			tmp[0] = 0xcc;
			tmp[1] = x;
			_strm.Write (tmp, 0, 2);
		}

		public void Write (ushort x)
		{
			byte[] tmp = _tmp;
			tmp[0] = 0xcd;
			tmp[1] = (byte)(x >> 8);
			tmp[2] = (byte)x;
			_strm.Write (tmp, 0, 3);
		}

		public void Write (char x)
		{
			Write ((ushort)x);
		}

		public void Write (uint x)
		{
			byte[] tmp = _tmp;
			tmp[0] = 0xce;
			tmp[1] = (byte)(x >> 24);
			tmp[2] = (byte)(x >> 16);
			tmp[3] = (byte)(x >>  8);
			tmp[4] = (byte)x;
			_strm.Write (tmp, 0, 5);
		}

		public void Write (ulong x)
		{
			byte[] tmp = _tmp;
			tmp[0] = 0xcf;
			tmp[1] = (byte)(x >> 56);
			tmp[2] = (byte)(x >> 48);
			tmp[3] = (byte)(x >> 40);
			tmp[4] = (byte)(x >> 32);
			tmp[5] = (byte)(x >> 24);
			tmp[6] = (byte)(x >> 16);
			tmp[7] = (byte)(x >>  8);
			tmp[8] = (byte)x;
			_strm.Write (tmp, 0, 9);
		}

		public void Write (sbyte x)
		{
			byte[] tmp = _tmp;
			tmp[0] = 0xd0;
			tmp[1] = (byte)x;
			_strm.Write (tmp, 0, 2);
		}

		public void Write (short x)
		{
			byte[] tmp = _tmp;
			tmp[0] = 0xd1;
			tmp[1] = (byte)(x >> 8);
			tmp[2] = (byte)x;
			_strm.Write (tmp, 0, 3);
		}

		public void Write (int x)
		{
			byte[] tmp = _tmp;
			tmp[0] = 0xd2;
			tmp[1] = (byte)(x >> 24);
			tmp[2] = (byte)(x >> 16);
			tmp[3] = (byte)(x >> 8);
			tmp[4] = (byte)x;
			_strm.Write (tmp, 0, 5);
		}

		public void Write (long x)
		{
			byte[] tmp = _tmp;
			tmp[0] = 0xd3;
			tmp[1] = (byte)(x >> 56);
			tmp[2] = (byte)(x >> 48);
			tmp[3] = (byte)(x >> 40);
			tmp[4] = (byte)(x >> 32);
			tmp[5] = (byte)(x >> 24);
			tmp[6] = (byte)(x >> 16);
			tmp[7] = (byte)(x >> 8);
			tmp[8] = (byte)x;
			_strm.Write (tmp, 0, 9);
		}

		public void WriteNil ()
		{
			_strm.WriteByte (0xc0);
		}

		public void Write (bool x)
		{
			_strm.WriteByte ((byte)(x ? 0xc3 : 0xc2));
		}

		public void Write (float x)
		{
			byte[] raw = BitConverter.GetBytes (x); // unsafeコードを使う?
			byte[] tmp = _tmp;

			tmp[0] = 0xca;
			if (BitConverter.IsLittleEndian) {
				tmp[1] = raw[3];
				tmp[2] = raw[2];
				tmp[3] = raw[1];
				tmp[4] = raw[0];
			} else {
				tmp[1] = raw[0];
				tmp[2] = raw[1];
				tmp[3] = raw[2];
				tmp[4] = raw[3];
			}
			_strm.Write (tmp, 0, 5);
		}

		public void Write (double x)
		{
			byte[] raw = BitConverter.GetBytes (x); // unsafeコードを使う?
			byte[] tmp = _tmp;

			tmp[0] = 0xcb;
			if (BitConverter.IsLittleEndian) {
				tmp[1] = raw[7];
				tmp[2] = raw[6];
				tmp[3] = raw[5];
				tmp[4] = raw[4];
				tmp[5] = raw[3];
				tmp[6] = raw[2];
				tmp[7] = raw[1];
				tmp[8] = raw[0];
			} else {
				tmp[1] = raw[0];
				tmp[2] = raw[1];
				tmp[3] = raw[2];
				tmp[4] = raw[3];
				tmp[5] = raw[4];
				tmp[6] = raw[5];
				tmp[7] = raw[6];
				tmp[8] = raw[7];
			}
			_strm.Write (tmp, 0, 9);
		}
		
		public void Write (byte[] bytes)
		{
			//WriteRawHeader (bytes.Length);
			WriteBinHeader (bytes.Length);
			_strm.Write (bytes, 0, bytes.Length);
		}

		public void WriteRawHeader (int N)
		{
			WriteLengthHeader (N, 32, 0xa0, 0xda, 0xdb);
		}

		public void WriteBinHeader (int N)
		{
			var t = _tmp;
			if (N < 0xFF) {
				t [0] = 0xc4;
				t [1] = (byte)N;
				_strm.Write (t, 0, 2);
			} else if (N <= 0xFFFF) {
				t [0] = 0xc5;
				t [1] = (byte)(N >> 8);
				t [2] = (byte)(N & 0xff);
				_strm.Write (t, 0, 3);
			} else {
				t [0] = 0xc6;
				t [1] = (byte)(N >> 24);
				t [2] = (byte)(N >> 16);
				t [3] = (byte)(N >> 8);
				t [4] = (byte)(N & 0xff);
				_strm.Write (t, 0, 5);
			}
		}

		public void WriteArrayHeader (int N)
		{
			WriteLengthHeader (N, 16, 0x90, 0xdc, 0xdd);
		}

		public void WriteMapHeader (int N)
		{
			WriteLengthHeader (N, 16, 0x80, 0xde, 0xdf);
		}

		void WriteLengthHeader (int N, int fix_length, byte fix_prefix, byte len16bit_prefix, byte len32bit_prefix)
		{
			if (N < fix_length) {
				_strm.WriteByte ((byte)(fix_prefix | N));
			} else {
				byte[] tmp = _tmp;
				int header_len;
				if (N < 0x10000) {
					tmp[0] = len16bit_prefix;
					tmp[1] = (byte)(N >> 8);
					tmp[2] = (byte)N;
					header_len = 3;
				} else {
					tmp[0] = len32bit_prefix;
					tmp[1] = (byte)(N >> 24);
					tmp[2] = (byte)(N >> 16);
					tmp[3] = (byte)(N >>  8);
					tmp[4] = (byte)N;
					header_len = 5;
				}
				_strm.Write (tmp, 0, header_len);
			}
		}

		public void Write (string x)
		{
			Write (x, false);
		}
		
		public void Write (string x, bool highProbAscii)
		{
			Write (x, _buf, highProbAscii);
		}

		public void Write (string x, byte[] buf)
		{
			Write (x, buf, false);
		}

		public void Write (string x, byte[] buf, bool highProbAscii)
		{
			Encoder encoder = _encoder;
			//fixed (char *pstr = x)
			//fixed (byte *pbuf = buf) {
			char[] str = x.ToCharArray();
				if (highProbAscii && x.Length <= buf.Length) {
					bool isAsciiFullCompatible = true;
					for (int i = 0; i < x.Length; i ++) { 
						//int v = (int)pstr[i];
						int v = (int)(x[i]);
						if (v > 0x7f) {
							isAsciiFullCompatible = false;
							break;
						}
						buf[i] = (byte)v;
					}
					if (isAsciiFullCompatible) {
						WriteRawHeader (x.Length);
						_strm.Write (buf, 0, x.Length);
						return;
					}
				}

				//WriteRawHeader (encoder.GetByteCount (pstr, x.Length, true));
				WriteRawHeader (encoder.GetByteCount (str, 0, x.Length, true));
				int str_len = x.Length;
				//char *p = pstr;
				int convertedChars, bytesUsed;
				bool completed = true;
				int j = 0;
				while (str_len > 0 || !completed) {
					//encoder.Convert (p, str_len, pbuf, buf.Length, false, out convertedChars, out bytesUsed, out completed);
					encoder.Convert (str, j, str_len, buf, 0, buf.Length, false, out convertedChars, out bytesUsed, out completed);
					_strm.Write (buf, 0, bytesUsed);
					str_len -= convertedChars;
					//p += convertedChars;
					j += convertedChars;
				}
			//}
		}

		private void writeFloat(float x) {
			byte[] raw = BitConverter.GetBytes (x); // unsafeコードを使う?
			byte[] tmp = _tmp;
			if (BitConverter.IsLittleEndian) {
				tmp[1] = raw[3];
				tmp[2] = raw[2];
				tmp[3] = raw[1];
				tmp[4] = raw[0];
			} else {
				tmp[1] = raw[0];
				tmp[2] = raw[1];
				tmp[3] = raw[2];
				tmp[4] = raw[3];
			}
			_strm.Write (tmp, 0, 4);
		}

		public void Write (UnityEngine.Vector2 v) {
			WriteExtHeader(0x57, 8);
			writeFloat(v.x);
			writeFloat(v.y);
		}
		public void Write (UnityEngine.Vector3 v) {
			WriteExtHeader(0x56, 12);
			writeFloat(v.x);
			writeFloat(v.y);
			writeFloat(v.z);
		}
		public void Write (UnityEngine.Quaternion q) {
			WriteExtHeader(0x51, 16);
			writeFloat(q.w);
			writeFloat(q.x);
			writeFloat(q.y);
			writeFloat(q.z);
		}
		public void WriteExtHeader(byte type, int length) {
			var t = _tmp;

			switch (length) {
			case 1:
				_strm.WriteByte(0xd4);
				break;
			case 2:
				_strm.WriteByte(0xd5);
				break;
			case 4:
				_strm.WriteByte(0xd6);
				break;
			case 8:
				_strm.WriteByte(0xd7);
				break;
			case 16:
				_strm.WriteByte(0xd8);
				break;
			default:
				if (length <= 0xff) {
					t[0] = 0xc7;
					t[1] = (byte)length;
					_strm.Write(t, 0, 2);
				} else if (length <= 0xffff) {
					t[0] = 0xc8;
					t[1] = (byte)(length >> 8);
					t[2] = (byte)(length & 0xff);
					_strm.Write(t, 0, 3);
				} else {
					t[0] = 0xc9;
					t[1] = (byte)(length >> 24);
					t[2] = (byte)(length >> 16);
					t[3] = (byte)(length >> 8);
					t[4] = (byte)(length & 0xff);
					_strm.Write(t, 0, 5);
				}
				break;
			}
		}
		public void Write (Ext ex) {
			WriteExtHeader(ex.Type, ex.Data.Length);
			_strm.Write(ex.Data, 0, ex.Data.Length);
		}
	}
}
