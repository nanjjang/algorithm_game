using System;
using Wizard.Content;
using Wizard.Core;

namespace Wizard.Sim.Spell.Compile
{
    // CONTRACTS 3.2 "SpellTable 은 세계 상태다"의 표 자체. 세계(SimWorld)가 하나씩 가진다(서버는 여러 세계를 동시에 돌린다).
    //   - 항목 = 원천(CompileSource). 컴파일 결과(CompiledSpell)는 원천에서 다시 만들 수 있는 캐시다
    //   - 핸들 = 추가 순서의 색인(0부터). 같은 원천(정규 바이트가 같다)은 새 항목을 만들지 않고 있던 핸들을 준다(앞에서부터 선형으로 찾는다)
    //   - 표는 추가만 한다. 스냅숏에는 길이만 들어가고 복원은 그 길이로 줄인다(Truncate)
    //   - WriteState 와 상태 해시에는 길이와 모든 원천이 들어간다(Write). ReadState 는 원천에서 다시 컴파일한다(Read)
    // 새 핸들(Add)은 세계 생성과 Step 0단계에서만 생긴다(그때만 할당한다). 실행 경로(5~10단계)는 Get 만 부르고 할당하지 않는다.
    // 세계에 잇는 자리는 SimWorld.SpellTable.cs 다.
    internal sealed class SpellTableCore
    {
        private const int InitialCapacity = 8;   // 자료구조 크기(판마다 출발 조립 몇 개)

        private CompileSource?[] _sources = new CompileSource?[InitialCapacity];
        private CompiledSpell?[] _spells = new CompiledSpell?[InitialCapacity];
        // _chain[i] = 항목 0..i 의 원천 해시를 차례로 이은 값. 스냅숏이 "같은 표의 앞머리인가"를 값 하나로 확인한다(PrefixHash)
        private ulong[] _chain = new ulong[InitialCapacity];
        private int _count;

        public int Count => _count;

        // 원천의 핸들. 없으면 컴파일해 끝에 더한다(세계 생성, Step 0단계에서만 부른다).
        // 팩과 규칙은 부르는 쪽(세계)이 준다: 표는 세계의 필드 초기화 때 만들어지고 그때는 팩을 모른다. 한 표에는 언제나 같은 팩과 규칙을 준다
        public SpellHandle Add(CompileSource source, ContentDb db, in CompileRules rules)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }
            if (db == null)
            {
                throw new ArgumentNullException(nameof(db));
            }
            for (int i = 0; i < _count; i++)
            {
                if (_sources[i]!.SameAs(source))
                {
                    return new SpellHandle(i);
                }
            }
            if (_count == _sources.Length)
            {
                Array.Resize(ref _sources, _count * 2);
                Array.Resize(ref _spells, _count * 2);
                Array.Resize(ref _chain, _count * 2);
            }
            CompiledSpell spell = SpellCompilerCore.Compile(db, source, rules);
            _sources[_count] = source;
            _spells[_count] = spell;
            _chain[_count] = Chain(PrefixHash(_count), spell.Hash);
            return new SpellHandle(_count++);
        }

        public bool IsValidHandle(SpellHandle handle) => handle.Value >= 0 && handle.Value < _count;

        // 실행 경로가 부른다. 할당하지 않는다
        public CompiledSpell Get(SpellHandle handle)
        {
            if (!IsValidHandle(handle))
            {
                throw new SimInvariantException("spell table: handle out of range");
            }
            return _spells[handle.Value]!;
        }

        public CompileSource SourceAt(SpellHandle handle)
        {
            Get(handle);
            return _sources[handle.Value]!;
        }

        // 앞 length 개 항목의 지문(원천 해시를 차례로 이은 값). 길이 0은 0. 할당하지 않는다
        public ulong PrefixHash(int length)
        {
            if (length < 0 || length > _count)
            {
                throw new SimInvariantException("spell table: prefix length beyond the table");
            }
            return length == 0 ? 0UL : _chain[length - 1];
        }

        // LoadSnapshot: 길이를 저장한 값으로 줄인다. 표는 추가만 하므로 저장 때보다 길 수는 있어도 짧을 수는 없다
        public void Truncate(int length)
        {
            if (length < 0 || length > _count)
            {
                throw new SimInvariantException("spell table: snapshot length beyond the table");
            }
            for (int i = length; i < _count; i++)
            {
                _sources[i] = null;
                _spells[i] = null;
                _chain[i] = 0UL;
            }
            _count = length;
        }

        // 절 내용(정규 직렬화. 해시도 같은 함수로 센다): count, 항목마다 CompileSource 의 정규 바이트(핸들 순)
        public void Write(ref StateWriter w)
        {
            w.Count(_count);
            for (int i = 0; i < _count; i++)
            {
                _sources[i]!.Write(ref w);
            }
        }

        // 받은 원천에서 다시 컴파일해 표 전체를 덮어쓴다. 같은 원천이 두 번 나오면 표의 불변식이 깨진 것이다
        public void Read(ref StateReader r, ContentDb db, in CompileRules rules)
        {
            int n = r.Count();
            if (n < 0)
            {
                throw new SimInvariantException("spell table: negative count");
            }
            var sources = new CompileSource[n];
            for (int i = 0; i < n; i++)
            {
                sources[i] = CompileSource.Read(ref r);
                for (int k = 0; k < i; k++)
                {
                    if (sources[k].SameAs(sources[i]))
                    {
                        throw new SimInvariantException("spell table: duplicate source");
                    }
                }
            }
            Truncate(0);
            foreach (CompileSource s in sources)
            {
                Add(s, db, rules);
            }
        }

        // 앞머리 지문에 항목 하나의 원천 해시를 잇는다(FNV-1a 64 의 섞기를 StateWriter 로 한다. 순서가 바뀌면 값이 달라진다)
        private static ulong Chain(ulong prefix, ulong sourceHash)
        {
            StateWriter w = StateWriter.ForHash();
            w.U64(prefix);
            w.U64(sourceHash);
            return w.Hash;
        }
    }
}
