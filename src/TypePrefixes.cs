﻿//
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

namespace MsgPack
{
	public enum TypePrefixes : byte
	{
		PositiveFixNum = 0x00, // 0x00 - 0x7f
		NegativeFixNum = 0xe0, // 0xe0 - 0xff

		Nil = 0xc0,
		False = 0xc2,
		True = 0xc3,
		Float = 0xca,
		Double = 0xcb,
		UInt8 = 0xcc,
		UInt16 = 0xcd,
		UInt32 = 0xce,
		UInt64 = 0xcf,
		Int8 = 0xd0,
		Int16 = 0xd1,
		Int32 = 0xd2,
		Int64 = 0xd3,
		Array16 = 0xdc,
		Array32 = 0xdd,
		Map16 = 0xde,
		Map32 = 0xdf,

		FixStr = 0xa0,   // 0xa0 - 0xbf
		FixArray = 0x90, // 0x90 - 0x9f
		FixMap = 0x80,   // 0x80 - 0x8f

		Bin8 = 0xc4,
		Bin16 = 0xc5,
		Bin32 = 0xc6,
		Ext8 = 0xc7,
		Ext16 = 0xc8,
		Ext32 = 0xc9,
		FixExt8 = 0xd4,
		FixExt16 = 0xd5,
		FixExt32 = 0xd6,
		FixExt64 = 0xd7,
		FixExt128 = 0xd8,
		Str8 = 0xd9,
		Str16 = 0xda,
		Str32 = 0xdb,
	}
}
