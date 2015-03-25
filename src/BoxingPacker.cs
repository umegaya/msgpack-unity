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
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace MsgPack
{
	public struct Ext {
		public byte Type;
		public byte[] Data;

		public Ext(byte type, byte[] data) {
			Type = type;
			Data = data;
		}
	}

	public class BoxingPacker
	{
		static Type KeyValuePairDefinitionType;

		static BoxingPacker ()
		{
			KeyValuePairDefinitionType = typeof (KeyValuePair<object,object>).GetGenericTypeDefinition ();
		}

		public void Pack (Stream strm, object o)
		{
			MsgPackWriter writer = new MsgPackWriter (strm);
			Pack (writer, o);
		}

		public byte[] Pack (object o)
		{
			using (MemoryStream ms = new MemoryStream ()) {
				Pack (ms, o);
				return ms.ToArray ();
			}
		}

		void Pack (MsgPackWriter writer, object o)
		{
			if (o == null) {
				writer.WriteNil ();
				return;
			}

			Type t = o.GetType ();
			if (t.IsPrimitive) {
				if (t.Equals (typeof (int))) writer.Write ((int)o);
				else if (t.Equals (typeof (uint))) writer.Write ((uint)o);
				else if (t.Equals (typeof (float))) writer.Write ((float)o);
				else if (t.Equals (typeof (double))) writer.Write ((double)o);
				else if (t.Equals (typeof (long))) writer.Write ((long)o);
				else if (t.Equals (typeof (ulong))) writer.Write ((ulong)o);
				else if (t.Equals (typeof (bool))) writer.Write ((bool)o);
				else if (t.Equals (typeof (byte))) writer.Write ((byte)o);
				else if (t.Equals (typeof (sbyte))) writer.Write ((sbyte)o);
				else if (t.Equals (typeof (short))) writer.Write ((short)o);
				else if (t.Equals (typeof (ushort))) writer.Write ((ushort)o);
				else throw new NotSupportedException ();  // char?
				return;
			}

			IDictionary dic = o as IDictionary;
			if (dic != null) {
				writer.WriteMapHeader (dic.Count);
				foreach (System.Collections.DictionaryEntry e in dic) {
					Pack (writer, e.Key);
					Pack (writer, e.Value);
				}
				return;
			}
			
			if (t.IsArray) {
				Array ary = (Array)o;
				Type et = t.GetElementType ();

				// KeyValuePair<K,V>[] (Map Type)
				if (et.IsGenericType && et.GetGenericTypeDefinition ().Equals (KeyValuePairDefinitionType)) {
					PropertyInfo propKey = et.GetProperty ("Key");
					PropertyInfo propValue = et.GetProperty ("Value");
					writer.WriteMapHeader (ary.Length);
					for (int i = 0; i < ary.Length; i ++) {
						object e = ary.GetValue (i);
						Pack (writer, propKey.GetValue (e, null));
						Pack (writer, propValue.GetValue (e, null));
					}
					return;
				}

				// Array
				writer.WriteArrayHeader (ary.Length);
				for (int i = 0; i < ary.Length; i ++)
					Pack (writer, ary.GetValue (i));
				return;
			}
		}

		public IEnumerator Unpack (Stream strm)
		{
			MsgPackReader reader = new MsgPackReader (strm);
			return Unpack (reader);
		}

		public IEnumerator Unpack (byte[] buf, int offset, int size)
		{
			using (MemoryStream ms = new MemoryStream (buf, offset, size)) {
				return Unpack (ms);
			}
		}

		public IEnumerator Unpack (byte[] buf)
		{
			return Unpack (buf, 0, buf.Length);
		}

		IEnumerator Unpack (MsgPackReader reader)
		{
			object obj;
			reader.Read ();
			switch (reader.Type) {
				case TypePrefixes.PositiveFixNum:
				case TypePrefixes.NegativeFixNum:
				case TypePrefixes.Int32:
					obj = reader.ValueSigned;
					break;
				case TypePrefixes.Int8:
					obj = (sbyte)reader.ValueSigned;
					break;
				case TypePrefixes.Int16:
					obj = (Int16)reader.ValueSigned;
					break;
				case TypePrefixes.Int64:
					obj = reader.ValueSigned64;
					break;

				case TypePrefixes.UInt8:
					obj = (byte)reader.ValueUnsigned;
					break;
				case TypePrefixes.UInt16:
					obj = (UInt16)reader.ValueUnsigned;
					break;
				case TypePrefixes.UInt32:
					obj = reader.ValueUnsigned;
					break;
				case TypePrefixes.UInt64:
					obj = reader.ValueUnsigned64;
					break;
				case TypePrefixes.True:
					obj = true;
					break;
				case TypePrefixes.False:
					obj = false;
					break;
				case TypePrefixes.Float:
					obj = reader.ValueFloat;
					break;
				case TypePrefixes.Double:
					obj = reader.ValueDouble;
					break;
				case TypePrefixes.Nil:
					obj = null;
					break;
				case TypePrefixes.FixStr:
				case TypePrefixes.Str8:
				case TypePrefixes.Str16:
				case TypePrefixes.Str32:
					byte[] str = new byte[reader.Length];
					reader.ReadStream (str, reader.Length);
					obj = (object)reader.StringifyBytes(str);
					break;
				case TypePrefixes.Bin8:
				case TypePrefixes.Bin16:
				case TypePrefixes.Bin32:
					byte[] tmp = new byte[reader.Length];
					reader.ReadStream (tmp, reader.Length);
					obj = (object)tmp;
					break;
				case TypePrefixes.FixArray:
				case TypePrefixes.Array16:
				case TypePrefixes.Array32:
					object[] ary = new object[reader.Length];
					for (int i = 0; i < ary.Length; i ++)
						ary[i] = Unpack (reader);
					obj = (object)ary;
					break;
				case TypePrefixes.FixMap:
				case TypePrefixes.Map16:
				case TypePrefixes.Map32:
					Dictionary<object, object> dic = new Dictionary<object, object> ((int)reader.Length);
					int count = (int)reader.Length;
					for (int i = 0; i < count; i ++) {
						object k = Unpack (reader);
						object v = Unpack (reader);
						dic.Add (k, v);
					}
					obj = (object)dic;
					break;
				case TypePrefixes.Ext8:
				case TypePrefixes.Ext16:
				case TypePrefixes.Ext32:
					byte[] et = new byte[1];
					reader.ReadExtType (et);
					switch (et[0]) {
					case 0x51:
						if (reader.Length == 16) {
							float a, b, c, d;
							a = reader.ReadSingle ();
							b = reader.ReadSingle ();
							c = reader.ReadSingle ();
							d = reader.ReadSingle ();
							obj = (object)new UnityEngine.Quaternion (b, c, d, a);
						}
						break;
					case 0x56:
						if (reader.Length == 12) {
							float a, b, c;
							a = reader.ReadSingle ();
							b = reader.ReadSingle ();
							c = reader.ReadSingle ();
							obj = (object)new UnityEngine.Vector3 (a, b, c);
						}
						break;
					case 0x57:
						if (reader.Length == 8) {
							float a, b;
							a = reader.ReadSingle ();
							b = reader.ReadSingle ();
							obj = (object)new UnityEngine.Vector2 (a, b);
						}
						break;
					}
					var data = new byte[reader.Length];
					reader.ReadStream (data, reader.Length);
					obj = (object)new Ext (et[0], data);
					break;
				default:
					throw new FormatException ();
			}
			yield return obj;
		}
	}
}
