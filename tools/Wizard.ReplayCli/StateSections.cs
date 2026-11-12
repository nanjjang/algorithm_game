using System;
using System.Collections.Generic;
using System.Reflection;
using Wizard.Core;
using Wizard.Sim;

namespace Wizard.ReplayCli
{
    // 세계 상태를 절(CONTRACTS 3.2 "상태 절과 해시")로 나눠 본다. 해시가 갈라졌을 때 어느 절인지 알려 주고(PIPELINE 7.3),
    // 되감기·직렬화 퍼저가 절마다 대조한다.
    //
    // 절 목록은 코어의 절 열거형(Wizard.Sim.StateSection)에서 읽는다. 절을 더하면 목록이 저절로 늘어난다(이 파일을 고치지 않는다).
    // 절의 내용은 그 모듈의 상태 훅 State_Write_<절 이름>(ref StateWriter)을 그대로 불러 얻는다(계약의 훅 이름. CONTRACTS 3.2).
    // 훅은 SimWorld 의 비공개 부분 메서드라 반사로 찾는다. 구현이 없는 부분 메서드는 컴파일에서 사라지므로 "훅 없음" = 그 절은 내용이 없다.
    // 절의 틀: 스칼라 절은 세계가 (절 id) 를 쓰고 훅이 내용을 쓴다. 풀 절은 훅이 틀까지 쓴다(비면 아무것도 쓰지 않는다).
    // 어느 쪽인지는 실제 WriteState 바이트와 맞춰 가며 정하고, 다 맞춘 뒤 절을 이어 붙인 바이트가 WriteState 와 한 바이트도 다르지 않은지 확인한다.
    // 맞지 않으면(순서표가 바뀌었다, 훅 이름이 계약과 다르다) 추측하지 않고 InvalidOperationException 이다.
    public static class StateSections
    {
        private const string SectionEnumName = "Wizard.Sim.StateSection";
        private const string WritePrefix = "State_Write_";
        private const string ReadPrefix = "State_Read_";
        private const string SavePrefix = "Snapshot_Save_";
        private const string LoadPrefix = "Snapshot_Load_";
        private const int IdBytes = 2;
        private const int ByteBits = 8;

        private delegate void SectionWriter(SimWorld world, ref StateWriter w);

        private static readonly Lazy<StateSectionInfo[]> ListLazy = new Lazy<StateSectionInfo[]>(BuildList);

        // 절 id 오름차순(= 직렬화 순서. SimWorld.State.cs 의 순서표)
        public static IReadOnlyList<StateSectionInfo> List => ListLazy.Value;

        public static StateSectionInfo Find(ushort id)
        {
            foreach (StateSectionInfo s in List)
            {
                if (s.Id == id)
                {
                    return s;
                }
            }
            throw new KeyNotFoundException("no state section " + id);
        }

        // 세계 상태를 절마다 나눈다. 틱 밖에서 부른다(WriteState 와 같은 조건)
        public static StateImage Capture(SimWorld world)
        {
            if (world == null)
            {
                throw new ArgumentNullException(nameof(world));
            }
            var whole = new ByteWriter(4096);
            world.WriteState(whole);
            byte[] all = whole.ToArray();

            var sections = new SectionBytes[List.Count];
            int pos = 0;
            var content = new ByteWriter(1024);
            for (int i = 0; i < List.Count; i++)
            {
                StateSectionInfo info = List[i];
                content.Reset();
                if (info.Writer != null)
                {
                    StateWriter w = StateWriter.ForBytes(content);
                    ((SectionWriter)info.Writer)(world, ref w);
                }
                ReadOnlySpan<byte> body = content.Written;
                byte[] scalar = Framed(info.Id, body);
                if (Matches(all, pos, scalar))
                {
                    sections[i] = new SectionBytes(info, SectionShape.Scalar, true, scalar);
                    pos += scalar.Length;
                }
                else if (body.Length == 0)
                {
                    sections[i] = new SectionBytes(info, SectionShape.Pool, false, Array.Empty<byte>());
                }
                else if (body.Length >= IdBytes && ReadId(body) == info.Id && Matches(all, pos, body))
                {
                    sections[i] = new SectionBytes(info, SectionShape.Pool, true, body.ToArray());
                    pos += body.Length;
                }
                else
                {
                    throw new InvalidOperationException("state section " + info.Name + "(" + info.Id + ") does not line up with WriteState at byte " + pos +
                        " (the section order or a hook name differs from CONTRACTS 3.2)");
                }
            }
            if (pos != all.Length)
            {
                throw new InvalidOperationException("WriteState has " + (all.Length - pos) + " bytes after the last known state section " +
                    "(a section the StateSection enum does not list)");
            }
            return new StateImage(all, sections);
        }

        private static StateSectionInfo[] BuildList()
        {
            Type? enumType = typeof(SimWorld).Assembly.GetType(SectionEnumName, false);
            if (enumType == null || !enumType.IsEnum)
            {
                throw new InvalidOperationException(SectionEnumName + " not found in Wizard.Sim");
            }
            var list = new List<StateSectionInfo>();
            foreach (object value in Enum.GetValues(enumType))
            {
                ushort id = Convert.ToUInt16(value, System.Globalization.CultureInfo.InvariantCulture);
                string name = Enum.GetName(enumType, value)!;
                MethodInfo? write = Hook(WritePrefix + name);
                Delegate? writer = write?.CreateDelegate(typeof(SectionWriter));
                list.Add(new StateSectionInfo(id, name, writer, write != null, Hook(ReadPrefix + name) != null,
                                              Hook(SavePrefix + name) != null, Hook(LoadPrefix + name) != null));
            }
            list.Sort((a, b) => a.Id.CompareTo(b.Id));
            return list.ToArray();
        }

        private static MethodInfo? Hook(string name) =>
            typeof(SimWorld).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

        private static byte[] Framed(ushort id, ReadOnlySpan<byte> body)
        {
            var b = new byte[IdBytes + body.Length];
            b[0] = (byte)id;
            b[1] = (byte)(id >> ByteBits);
            body.CopyTo(b.AsSpan(IdBytes));
            return b;
        }

        private static ushort ReadId(ReadOnlySpan<byte> b) => (ushort)(b[0] | (b[1] << ByteBits));

        private static bool Matches(byte[] all, int pos, ReadOnlySpan<byte> part) =>
            pos + part.Length <= all.Length && all.AsSpan(pos, part.Length).SequenceEqual(part);
    }
}
