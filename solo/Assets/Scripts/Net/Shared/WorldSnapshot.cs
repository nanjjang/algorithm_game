using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Wizard.Solo.Data;
using Wizard.Solo.Sim;

namespace Wizard.Solo.Net
{
    /// <summary>
    /// 서버 세계 → 클라이언트 "꼭두각시" 세계(Step하지 않는다)로 상태를 옮긴다(11-online.md 3절).
    /// 상태 클래스의 public 필드를 선언 순서대로 쓴다(리플렉션, 형식마다 한 번만 훑어 둔다). 그래서 상태에 필드가 늘어도 이 파일은 그대로다.
    /// - 기획 정의(…Def)는 id만 보내고 받는 쪽 IContentDb에서 다시 찾는다(<see cref="DefResolvers"/>). 모르는 Def는 보내지 않는다.
    /// - 모듈 속(object Scratch·Brain, CompiledSpell)은 보내지 않는다. 꼭두각시에서 그것이 필요한 표시(K 커맨드 미리보기)는 빈 채로 그린다.
    /// - 같은 자리의 객체는 다시 쓴다(목록은 같은 칸의 객체 위에 덮어쓴다). 보이는 쪽은 Id로 객체를 따라가므로 순서만 맞으면 된다.
    /// 2단계는 매번 전체를 보낸다. 차이만 보내기(델타)는 3단계에서 대역폭을 재고 더한다.
    /// </summary>
    public static class WorldSnapshot
    {
        // ------------------------------------------------------------ 세계

        public static void Write(NetWriter w, SimWorld world)
        {
            w.Int(world.Tick);
            w.Float(world.ArenaWidth);
            w.Float(world.ArenaHeight);
            w.Int(world.Players.Length);
            for (int i = 0; i < world.Players.Length; i++) WriteObject(w, world.Players[i], typeof(PlayerState));
            WriteValue(w, world.Enemies, typeof(List<EnemyState>));
            WriteValue(w, world.Projectiles, typeof(List<ProjectileEntity>));
            WriteValue(w, world.EnemyShots, typeof(List<EnemyShotEntity>));
            WriteValue(w, world.Zones, typeof(List<ZoneEntity>));
            WriteValue(w, world.Hazards, typeof(List<HazardEntity>));
            WriteValue(w, world.Obstacles, typeof(List<ObstacleEntity>));
            WriteValue(w, world.PlayerZones, typeof(List<PlayerZoneEntity>));
            WriteValue(w, world.Drops, typeof(List<DropEntity>));
            WriteValue(w, world.Run, typeof(RunState));
            WriteValue(w, world.Wave, typeof(WaveState));
            WriteValue(w, world.Boss, typeof(BossState));
            WriteObject(w, world.Starfall, typeof(StarfallState));
            WriteObject(w, world.Weather, typeof(WeatherState));
        }

        /// <summary>받은 스냅숏을 꼭두각시 세계에 덮어쓴다. 받기 전의 표시용 위치는 PrevPos에 남는다(스냅숏의 PrevPos가 덮는다).</summary>
        public static void Apply(NetReader r, SimWorld world)
        {
            var db = world.Content;
            world.Tick = r.Int();
            world.ArenaWidth = r.Float();
            world.ArenaHeight = r.Float();
            int n = r.Int();
            for (int i = 0; i < n; i++)
            {
                if (i < world.Players.Length) ReadObject(r, world.Players[i], typeof(PlayerState), db);
                else ReadObject(r, new PlayerState(), typeof(PlayerState), db);
            }
            ReadValue(r, world.Enemies, typeof(List<EnemyState>), db);
            ReadValue(r, world.Projectiles, typeof(List<ProjectileEntity>), db);
            ReadValue(r, world.EnemyShots, typeof(List<EnemyShotEntity>), db);
            ReadValue(r, world.Zones, typeof(List<ZoneEntity>), db);
            ReadValue(r, world.Hazards, typeof(List<HazardEntity>), db);
            ReadValue(r, world.Obstacles, typeof(List<ObstacleEntity>), db);
            ReadValue(r, world.PlayerZones, typeof(List<PlayerZoneEntity>), db);
            ReadValue(r, world.Drops, typeof(List<DropEntity>), db);
            world.Run = (RunState)ReadValue(r, world.Run, typeof(RunState), db) ?? new RunState();
            world.Wave = (WaveState)ReadValue(r, world.Wave, typeof(WaveState), db) ?? new WaveState();
            world.Boss = (BossState)ReadValue(r, world.Boss, typeof(BossState), db);
            ReadObject(r, world.Starfall, typeof(StarfallState), db);
            ReadObject(r, world.Weather, typeof(WeatherState), db);
        }

        // ------------------------------------------------------------ 기획 정의 다시 찾기

        static readonly Dictionary<Type, Func<IContentDb, string, object>> DefResolvers = new Dictionary<Type, Func<IContentDb, string, object>>
        {
            { typeof(CharacterDef), (db, id) => db.Character(id) },
            { typeof(EnemyDef), (db, id) => db.Enemy(id) },
            { typeof(EliteDef), (db, id) => db.Elite(id) },
            { typeof(BossDef), (db, id) => db.Boss(id) },
            { typeof(BlockDef), (db, id) => db.Block(id) },
            { typeof(ArtDef), (db, id) => db.Art(id) },
            { typeof(FrameDef), (db, id) => db.Frame(id) },
            { typeof(ModifierDef), (db, id) => db.Modifier(id) },
            { typeof(TodDef), (db, id) => db.Tod(id) },
        };

        // ------------------------------------------------------------ 형식 훑기

        enum Kind { Skip, Int, Long, ULong, Float, Bool, Byte, Short, Enum, String, Vector2, Struct, Class, Def, List, Array, HashSet, Queue, Dict }

        sealed class TypeInfo
        {
            public Kind Kind;
            public Type Elem;   // List·Array·HashSet·Queue의 원소, Dict의 열쇠
            public Type Elem2;  // Dict의 값
            public Type EnumUnder;
            public FieldInfo[] Fields; // Struct·Class
        }

        static readonly Dictionary<Type, TypeInfo> _types = new Dictionary<Type, TypeInfo>();

        static TypeInfo Info(Type t)
        {
            if (_types.TryGetValue(t, out var ti)) return ti;
            ti = new TypeInfo();
            _types[t] = ti; // 재귀 형식 대비 먼저 넣는다
            if (t == typeof(int)) ti.Kind = Kind.Int;
            else if (t == typeof(long)) ti.Kind = Kind.Long;
            else if (t == typeof(ulong)) ti.Kind = Kind.ULong;
            else if (t == typeof(float)) ti.Kind = Kind.Float;
            else if (t == typeof(bool)) ti.Kind = Kind.Bool;
            else if (t == typeof(byte)) ti.Kind = Kind.Byte;
            else if (t == typeof(short)) ti.Kind = Kind.Short;
            else if (t.IsEnum) { ti.Kind = Kind.Enum; ti.EnumUnder = Enum.GetUnderlyingType(t); }
            else if (t == typeof(string)) ti.Kind = Kind.String;
            else if (t == typeof(Vector2)) ti.Kind = Kind.Vector2;
            else if (DefResolvers.ContainsKey(t)) ti.Kind = Kind.Def;
            else if (t.IsArray) { ti.Kind = Kind.Array; ti.Elem = t.GetElementType(); }
            else if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(List<>)) { ti.Kind = Kind.List; ti.Elem = t.GetGenericArguments()[0]; }
            else if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(HashSet<>)) { ti.Kind = Kind.HashSet; ti.Elem = t.GetGenericArguments()[0]; }
            else if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Queue<>)) { ti.Kind = Kind.Queue; ti.Elem = t.GetGenericArguments()[0]; }
            else if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Dictionary<,>))
            {
                ti.Kind = Kind.Dict;
                var a = t.GetGenericArguments();
                ti.Elem = a[0];
                ti.Elem2 = a[1];
            }
            else if (IsStateType(t))
            {
                ti.Kind = t.IsValueType ? Kind.Struct : Kind.Class;
                var all = t.GetFields(BindingFlags.Public | BindingFlags.Instance);
                Array.Sort(all, (x, y) => x.MetadataToken.CompareTo(y.MetadataToken));
                var keep = new List<FieldInfo>();
                foreach (var f in all)
                {
                    if (f.Name.EndsWith("Scratch", StringComparison.Ordinal) || f.Name == "Brain") continue;
                    if (Info(f.FieldType).Kind == Kind.Skip) continue;
                    keep.Add(f);
                }
                ti.Fields = keep.ToArray();
            }
            else ti.Kind = Kind.Skip;

            // 원소가 보낼 수 없는 형식이면 모음 전체를 보내지 않는다.
            if ((ti.Kind == Kind.List || ti.Kind == Kind.Array || ti.Kind == Kind.HashSet || ti.Kind == Kind.Queue) && Info(ti.Elem).Kind == Kind.Skip) ti.Kind = Kind.Skip;
            if (ti.Kind == Kind.Dict && (Info(ti.Elem).Kind == Kind.Skip || Info(ti.Elem2).Kind == Kind.Skip)) ti.Kind = Kind.Skip;
            return ti;
        }

        /// <summary>시뮬 상태 형식(Wizard.Solo.Sim 이름공간의 클래스·구조체)만 펼친다. 모듈 속(CompiledSpell 등)은 이름으로 뺀다.</summary>
        static bool IsStateType(Type t)
        {
            if (t == typeof(object) || t == typeof(CompiledSpell)) return false;
            return t.Namespace == "Wizard.Solo.Sim" && (t.IsClass || (t.IsValueType && !t.IsPrimitive));
        }

        // ------------------------------------------------------------ 쓰기

        static void WriteObject(NetWriter w, object o, Type t)
        {
            var ti = Info(t);
            foreach (var f in ti.Fields) WriteValue(w, f.GetValue(o), f.FieldType);
        }

        static void WriteValue(NetWriter w, object v, Type t)
        {
            var ti = Info(t);
            switch (ti.Kind)
            {
                case Kind.Skip: return;
                case Kind.Int: w.Int((int)v); return;
                case Kind.Long: w.Long((long)v); return;
                case Kind.ULong: w.ULong((ulong)v); return;
                case Kind.Float: w.Float((float)v); return;
                case Kind.Bool: w.Bool((bool)v); return;
                case Kind.Byte: w.Byte((byte)v); return;
                case Kind.Short: w.Int((short)v); return;
                case Kind.Enum: w.Long(Convert.ToInt64(v)); return;
                case Kind.String: w.Str((string)v); return;
                case Kind.Vector2: w.Vec((Vector2)v); return;
                case Kind.Def: w.Str(v != null ? DefId(v) : null); return;
                case Kind.Struct: WriteObject(w, v, t); return;
                case Kind.Class:
                    w.Bool(v != null);
                    if (v != null) WriteObject(w, v, t);
                    return;
                case Kind.List:
                case Kind.Array:
                case Kind.HashSet:
                case Kind.Queue:
                    {
                        if (v == null) { w.Int(-1); return; }
                        // HashSet<T>는 ICollection(제네릭 아님)이 아니라서 세어서 쓴다. HashSet의 순서는 약속이 아니지만 받는 쪽도 집합이라 상관없다.
                        int count = 0;
                        foreach (var _ in (IEnumerable)v) count++;
                        w.Int(count);
                        foreach (var e in (IEnumerable)v) WriteValue(w, e, ti.Elem);
                        return;
                    }
                case Kind.Dict:
                    {
                        if (v == null) { w.Int(-1); return; }
                        var d = (IDictionary)v;
                        // 열쇠 순서를 고정한다(Dictionary의 순서는 약속이 아니다).
                        var keys = new List<object>();
                        foreach (var k in d.Keys) keys.Add(k);
                        keys.Sort((a, b) => Comparer<object>.Default.Compare(a, b));
                        w.Int(keys.Count);
                        foreach (var k in keys) { WriteValue(w, k, ti.Elem); WriteValue(w, d[k], ti.Elem2); }
                        return;
                    }
            }
        }

        static readonly Dictionary<Type, FieldInfo> _defIdField = new Dictionary<Type, FieldInfo>();

        static string DefId(object def)
        {
            var t = def.GetType();
            if (!_defIdField.TryGetValue(t, out var f))
            {
                f = t.GetField("Id", BindingFlags.Public | BindingFlags.Instance);
                _defIdField[t] = f;
            }
            return f != null ? f.GetValue(def) as string : null;
        }

        // ------------------------------------------------------------ 읽기

        static void ReadObject(NetReader r, object target, Type t, IContentDb db)
        {
            var ti = Info(t);
            foreach (var f in ti.Fields)
            {
                object cur = f.GetValue(target);
                object nv = ReadValue(r, cur, f.FieldType, db);
                if (f.IsInitOnly)
                {
                    // readonly 모음·객체는 자리 그대로 채웠다(ReadValue가 cur 안을 바꿨다).
                    if (!ReferenceEquals(nv, cur) && !f.FieldType.IsValueType) f.SetValue(target, nv);
                    continue;
                }
                f.SetValue(target, nv);
            }
        }

        /// <summary>값 하나를 읽는다. 참조 형식이면 가능한 한 cur를 다시 쓴다(돌려준 값을 필드에 넣는다).</summary>
        static object ReadValue(NetReader r, object cur, Type t, IContentDb db)
        {
            var ti = Info(t);
            switch (ti.Kind)
            {
                case Kind.Skip: return cur;
                case Kind.Int: return r.Int();
                case Kind.Long: return r.Long();
                case Kind.ULong: return r.ULong();
                case Kind.Float: return r.Float();
                case Kind.Bool: return r.Bool();
                case Kind.Byte: return r.Byte();
                case Kind.Short: return (short)r.Int();
                case Kind.Enum: return Enum.ToObject(t, r.Long());
                case Kind.String: return r.Str();
                case Kind.Vector2: return r.Vec();
                case Kind.Def:
                    {
                        string id = r.Str();
                        if (id == null || db == null) return null;
                        return DefResolvers[t](db, id);
                    }
                case Kind.Struct:
                    {
                        object boxed = cur ?? Activator.CreateInstance(t);
                        ReadObject(r, boxed, t, db);
                        return boxed;
                    }
                case Kind.Class:
                    {
                        if (!r.Bool()) return null;
                        object o = cur ?? Activator.CreateInstance(t, true);
                        ReadObject(r, o, t, db);
                        return o;
                    }
                case Kind.List:
                    {
                        int n = r.Int();
                        if (n < 0) return null;
                        var list = (IList)(cur ?? Activator.CreateInstance(t));
                        bool reuse = Info(ti.Elem).Kind == Kind.Class;
                        for (int i = 0; i < n; i++)
                        {
                            object old = reuse && i < list.Count ? list[i] : null;
                            object e = ReadValue(r, old, ti.Elem, db);
                            if (i < list.Count) list[i] = e; else list.Add(e);
                        }
                        while (list.Count > n) list.RemoveAt(list.Count - 1);
                        return list;
                    }
                case Kind.Array:
                    {
                        int n = r.Int();
                        if (n < 0) return null;
                        var arr = cur as Array;
                        if (arr == null || arr.Length != n) arr = Array.CreateInstance(ti.Elem, n);
                        bool reuse = Info(ti.Elem).Kind == Kind.Class;
                        for (int i = 0; i < n; i++) arr.SetValue(ReadValue(r, reuse ? arr.GetValue(i) : null, ti.Elem, db), i);
                        // readonly 배열의 길이가 다르면 새 배열은 필드에 못 넣는다 → 겹치는 칸만 원래 배열에 옮긴다.
                        if (cur is Array orig && !ReferenceEquals(orig, arr))
                        {
                            int m = Math.Min(orig.Length, arr.Length);
                            for (int i = 0; i < m; i++) orig.SetValue(arr.GetValue(i), i);
                        }
                        return arr;
                    }
                case Kind.HashSet:
                    {
                        int n = r.Int();
                        if (n < 0) return null;
                        object set = cur ?? Activator.CreateInstance(t);
                        t.GetMethod("Clear").Invoke(set, null);
                        var add = t.GetMethod("Add");
                        var arg = new object[1];
                        for (int i = 0; i < n; i++) { arg[0] = ReadValue(r, null, ti.Elem, db); add.Invoke(set, arg); }
                        return set;
                    }
                case Kind.Queue:
                    {
                        int n = r.Int();
                        if (n < 0) return null;
                        object q = cur ?? Activator.CreateInstance(t);
                        t.GetMethod("Clear").Invoke(q, null);
                        var enq = t.GetMethod("Enqueue");
                        var arg = new object[1];
                        for (int i = 0; i < n; i++) { arg[0] = ReadValue(r, null, ti.Elem, db); enq.Invoke(q, arg); }
                        return q;
                    }
                case Kind.Dict:
                    {
                        int n = r.Int();
                        if (n < 0) return null;
                        var d = (IDictionary)(cur ?? Activator.CreateInstance(t));
                        d.Clear();
                        for (int i = 0; i < n; i++)
                        {
                            object k = ReadValue(r, null, ti.Elem, db);
                            d[k] = ReadValue(r, null, ti.Elem2, db);
                        }
                        return d;
                    }
            }
            return cur;
        }
    }
}
