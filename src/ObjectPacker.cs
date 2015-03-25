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
using System.Collections.Generic;
using System.IO;
using System.Collections;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;

namespace MsgPack
{
	public class ObjectPacker
	{
		byte[] _buf = new byte[64];
		//Encoding _encoding = Encoding.UTF8;
		static Dictionary<Type, PackDelegate> PackerMapping;
		static Dictionary<Type, UnpackDelegate> UnpackerMapping;

		delegate void PackDelegate (ObjectPacker packer, MsgPackWriter writer, object o);
		delegate object UnpackDelegate (ObjectPacker packer, MsgPackReader reader);

		static ObjectPacker ()
		{
			PackerMapping = new Dictionary<Type, PackDelegate> ();
			UnpackerMapping = new Dictionary<Type, UnpackDelegate> ();

			PackerMapping.Add (typeof (string), StringPacker);
			UnpackerMapping.Add (typeof (string), StringUnpacker);
		}

		public byte[] Pack (object o)
		{
			using (MemoryStream ms = new MemoryStream ()) {
				Pack (ms, o);
				return ms.ToArray ();
			}
		}

		public void Pack (Stream strm, object o)
		{
			if (o != null && o.GetType ().IsPrimitive)
				throw new NotSupportedException ();
			MsgPackWriter writer = new MsgPackWriter (strm);
			Pack (writer, o);
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
				else if (t.Equals (typeof (char))) writer.Write ((ushort)(char)o);
				else throw new NotSupportedException ();
				return;
			}

			PackDelegate packer;
			if (PackerMapping.TryGetValue (t, out packer)) {
				packer (this, writer, o);
				return;
			}

			if (t.IsArray) {
				Array ary = (Array)o;
				writer.WriteArrayHeader (ary.Length);
				for (int i = 0; i < ary.Length; i ++)
					Pack (writer, ary.GetValue (i));
				return;
			}

			ReflectionCacheEntry entry = ReflectionCache.Lookup (t);
			writer.WriteMapHeader (entry.FieldMap.Count);
			foreach (KeyValuePair<string, FieldInfo> pair in entry.FieldMap) {
				writer.Write (pair.Key, _buf);
				object v = pair.Value.GetValue (o);
				if (pair.Value.FieldType.IsInterface && v != null) {
					writer.WriteArrayHeader (2);
					writer.Write (v.GetType().FullName);
				}
				Pack (writer, v);
			}
		}

		public IEnumerator Unpack<T> (byte[] buf)
		{
			yield return Unpack<T> (buf, 0, buf.Length);
		}

		public IEnumerator Unpack<T> (byte[] buf, int offset, int size)
		{
			using (MemoryStream ms = new MemoryStream (buf, offset, size)) {
				yield return Unpack<T> (ms);
			}
		}

		public IEnumerator Unpack<T> (Stream strm)
		{
			if (typeof (T).IsPrimitive)
				throw new NotSupportedException ();
			MsgPackReader reader = new MsgPackReader (strm);
			yield return (T)Unpack (reader, typeof (T));
		}

		public IEnumerator Unpack (Type type, byte[] buf)
		{
			return Unpack (type, buf, 0, buf.Length);
		}

		public IEnumerator Unpack (Type type, byte[] buf, int offset, int size)
		{
			using (MemoryStream ms = new MemoryStream (buf, offset, size)) {
				return Unpack (type, ms);
			}
		}

		public IEnumerator Unpack (Type type, Stream strm)
		{
			if (type.IsPrimitive)
				throw new NotSupportedException ();
			MsgPackReader reader = new MsgPackReader (strm);
			return Unpack (reader, type);
		}

		IEnumerator Unpack (MsgPackReader reader, Type t)
		{
			if (t.IsPrimitive) {
				reader.Read ();
				if (t.Equals (typeof (int)) && reader.IsSigned ()) yield return reader.ValueSigned;
				else if (t.Equals (typeof (uint)) && reader.IsUnsigned ()) yield return reader.ValueUnsigned;
				else if (t.Equals (typeof (float)) && reader.Type == TypePrefixes.Float) yield return reader.ValueFloat;
				else if (t.Equals (typeof (double)) && reader.Type == TypePrefixes.Double) yield return reader.ValueDouble;
				else if (t.Equals (typeof (long))) {
					if (reader.IsSigned64 ())
						yield return reader.ValueSigned64;
					if (reader.IsSigned ())
						yield return (long)reader.ValueSigned;
				} else if (t.Equals (typeof (ulong))) {
					if (reader.IsUnsigned64 ())
						yield return reader.ValueUnsigned64;
					if (reader.IsUnsigned ())
						yield return (ulong)reader.ValueUnsigned;
				} else if (t.Equals (typeof (bool)) && reader.IsBoolean ()) yield return (reader.Type == TypePrefixes.True);
				else if (t.Equals (typeof (byte)) && reader.IsUnsigned ()) yield return (byte)reader.ValueUnsigned;
				else if (t.Equals (typeof (sbyte)) && reader.IsSigned ()) yield return (sbyte)reader.ValueSigned;
				else if (t.Equals (typeof (short)) && reader.IsSigned ()) yield return (short)reader.ValueSigned;
				else if (t.Equals (typeof (ushort)) && reader.IsUnsigned ()) yield return (ushort)reader.ValueUnsigned;
				else if (t.Equals (typeof (char)) && reader.IsUnsigned ()) yield return (char)reader.ValueUnsigned;
				else throw new NotSupportedException ();
			}

			UnpackDelegate unpacker;
			if (UnpackerMapping.TryGetValue (t, out unpacker))
				yield return unpacker (this, reader);

			if (t.IsArray) {
				reader.Read ();
				if (!reader.IsArray () && reader.Type != TypePrefixes.Nil)
					throw new FormatException ();
				if (reader.Type == TypePrefixes.Nil)
					yield return null;
				Type et = t.GetElementType ();
				Array ary = Array.CreateInstance (et, (int)reader.Length);
				for (int i = 0; i < ary.Length; i ++)
					ary.SetValue (Unpack (reader, et), i);
				yield return ary;
			}

			reader.Read ();
			if (reader.Type == TypePrefixes.Nil)
				yield return null;
			if (t.IsInterface) {
				if (reader.Type != TypePrefixes.FixArray && reader.Length != 2)
					throw new FormatException ();
				reader.Read ();
				if (!reader.IsBinary ())
					throw new FormatException ();
				Reserve(reader.Length);
				reader.ReadRawBytes(_buf);
				string name = reader.StringifyBytes(_buf);
				t = Type.GetType (name);
				reader.Read ();
				if (reader.Type == TypePrefixes.Nil)
					throw new FormatException ();
			}
			if (!reader.IsMap ())
				throw new FormatException ();

			object o = FormatterServices.GetUninitializedObject (t);
			ReflectionCacheEntry entry = ReflectionCache.Lookup (t);
			int members = (int)reader.Length;
			for (int i = 0; i < members; i ++) {
				reader.Read ();
				if (!reader.IsBinary ())
					throw new FormatException ();
				Reserve(reader.Length);
				reader.ReadRawBytes(_buf);
				string name = reader.StringifyBytes(_buf);
				FieldInfo f;
				if (!entry.FieldMap.TryGetValue (name, out f))
					throw new FormatException ();
				f.SetValue (o, Unpack (reader, f.FieldType));
			}

			IDeserializationCallback callback = o as IDeserializationCallback;
			if (callback != null)
				callback.OnDeserialization (this);
			yield return o;
		}

		void Reserve (uint size)
		{
			if (_buf.Length < size)
				Array.Resize<byte> (ref _buf, (int)size);
		}

		static void StringPacker (ObjectPacker packer, MsgPackWriter writer, object o)
		{
			writer.Write (Encoding.UTF8.GetBytes ((string)o));
		}

		static IEnumerator StringUnpacker (ObjectPacker packer, MsgPackReader reader)
		{
			reader.Read ();
			if (reader.Type == TypePrefixes.Nil)
				yield return null;
			if (!reader.IsStr ())
				throw new FormatException ();
			byte[] buf = new byte[reader.Length];
			reader.ReadRawBytes(buf);
			yield return reader.StringifyBytes(buf);
		}
	}
}
