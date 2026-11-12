using System;
using System.Collections.Generic;
using System.Globalization;
using Wizard.Protocol.Crypto;
using Wizard.TestKit.Shared.Fuzz;

namespace Wizard.Protocol.Tests.Crypto.Shared
{
    // IAeadCipher 적합성 검사(M2-NET-02). 엔진 없는 netstandard2.1 + C# 9 이고 TestKit.Shared 의 퍼저 틀을 쓴다.
    // 같은 코드를 dotnet 테스트(tests/Wizard.Protocol.Tests/Crypto/)와 IL2CPP 플레이어의 머리 없는 실행이 부른다(T-44).
    // 플레이어에는 서버 구현이 없으므로 RunAll 은 구현 하나를 RFC 벡터, 경계 사례, 자기 왕복, 위조로 검사한다.
    // 두 구현의 교차 검사는 CheckCross·CheckTamper 에 봉인 쪽과 여는 쪽을 따로 넘긴다.
    //
    // 검사 함수는 문제를 문자열로 돌려준다(없으면 빈 목록 또는 null). 예외를 기대하는 검사 말고는 예외도 문제로 적는다.
    public static class AeadConformance
    {
        // 퍼저 주 씨앗("aead8439"). 바꾸면 모든 사례가 바뀐다
        public const ulong FuzzSeed = 0x6165616438343339UL;

        // 수락 조건: 무작위 키·nonce·길이 1만 건
        public const int FuzzCases = 10000;

        // 결투 패킷 한 개의 상한(CONTRACTS 10절의 1,200바이트)을 덮는다
        public const int MaxPlaintextBytes = 1200;

        public const int MaxAadBytes = 64;

        // 블록(64바이트)과 Poly1305 덧붙임(16바이트) 경계 근처의 길이
        private static readonly int[] EdgeLengths = { 0, 1, 15, 16, 17, 31, 32, 33, 63, 64, 65, 127, 128, 129, 1171, 1200 };

        public static FuzzOptions DefaultFuzzOptions() => new FuzzOptions(FuzzSeed, FuzzCases);

        public static FuzzGen<AeadCase> DefaultCases() => AeadCaseGen.Any(MaxPlaintextBytes, MaxAadBytes);

        // 플레이어(머리 없는 실행)가 부르는 묶음: 벡터 + 경계 + 자기 왕복 퍼징 + 위조 퍼징
        public static List<string> RunAll(string name, IAeadCipher cipher, IReadOnlyList<AeadVector> vectors)
        {
            var problems = new List<string>();
            problems.AddRange(CheckVectors(name, cipher, vectors));
            problems.AddRange(CheckEdges(name, cipher));
            AddFuzz(problems, name + " round trip", Fuzzer.Run(DefaultFuzzOptions(), DefaultCases(), c => CheckCross(cipher, cipher, c), null, Describe));
            AddFuzz(problems, name + " tamper", Fuzzer.Run(DefaultFuzzOptions(), DefaultCases(), c => CheckTamper(cipher, cipher, c), null, Describe));
            return problems;
        }

        public static string Describe(AeadCase c) => c.ToString();

        // RFC 8439 벡터: 봉인 결과가 RFC 의 암호문 ‖ 태그와 같고, 열면 RFC 의 평문이 나오고, 어느 한 비트라도 바뀌면 열리지 않는다.
        public static List<string> CheckVectors(string name, IAeadCipher cipher, IReadOnlyList<AeadVector> vectors)
        {
            var problems = new List<string>();
            if (vectors.Count == 0)
            {
                problems.Add(name + ": no RFC 8439 vectors were given");
            }
            foreach (AeadVector v in vectors)
            {
                string at = name + " " + v;
                try
                {
                    byte[] expected = v.CiphertextAndTag();
                    var sealedBytes = new byte[expected.Length];
                    cipher.Seal(v.Key, v.Nonce, v.Aad, v.Plaintext, sealedBytes);
                    Same(problems, at + " seal", expected, sealedBytes);

                    var opened = Sentinel(v.Plaintext.Length);
                    if (!cipher.Open(v.Key, v.Nonce, v.Aad, expected, opened))
                    {
                        problems.Add(at + ": Open returned false for the RFC ciphertext");
                    }
                    else
                    {
                        Same(problems, at + " open", v.Plaintext, opened);
                    }

                    // 바꿀 자리: 암호문 첫·끝 바이트, 태그 첫·끝 바이트, AAD, nonce, 키
                    CheckRejected(problems, at + " ciphertext[0]", cipher, v.Key, v.Nonce, v.Aad, Flip(expected, 0, 0x01));
                    CheckRejected(problems, at + " ciphertext[last]", cipher, v.Key, v.Nonce, v.Aad, Flip(expected, v.Ciphertext.Length - 1, 0x80));
                    CheckRejected(problems, at + " tag[0]", cipher, v.Key, v.Nonce, v.Aad, Flip(expected, v.Ciphertext.Length, 0x01));
                    CheckRejected(problems, at + " tag[15]", cipher, v.Key, v.Nonce, v.Aad, Flip(expected, expected.Length - 1, 0x80));
                    if (v.Aad.Length > 0)
                    {
                        CheckRejected(problems, at + " aad[0]", cipher, v.Key, v.Nonce, Flip(v.Aad, 0, 0x01), expected);
                    }
                    CheckRejected(problems, at + " aad appended", cipher, v.Key, v.Nonce, Append(v.Aad, 0x00), expected);
                    CheckRejected(problems, at + " nonce[11]", cipher, v.Key, Flip(v.Nonce, AeadSizes.NonceBytes - 1, 0x01), v.Aad, expected);
                    CheckRejected(problems, at + " key[0]", cipher, Flip(v.Key, 0, 0x01), v.Nonce, v.Aad, expected);
                    CheckRejected(problems, at + " truncated", cipher, v.Key, v.Nonce, v.Aad, Slice(expected, 0, expected.Length - 1));
                }
                catch (Exception ex)
                {
                    problems.Add(at + ": threw " + ex.GetType().FullName + ": " + ex.Message);
                }
            }
            return problems;
        }

        // 경계 사례: 길이 경계의 자기 왕복, 짧은 입력, 인자 오류, 제자리 봉인·열기, 같은 입력 두 번이면 같은 출력.
        public static List<string> CheckEdges(string name, IAeadCipher cipher)
        {
            var problems = new List<string>();
            byte[] key = Pattern(AeadSizes.KeyBytes, 3);
            byte[] nonce = Pattern(AeadSizes.NonceBytes, 5);
            byte[] aad = Pattern(13, 7);
            foreach (int len in EdgeLengths)
            {
                string at = name + " edge length " + len.ToString(CultureInfo.InvariantCulture);
                var c = new AeadCase(key, nonce, aad, Pattern(len, 11), len, 0x10);
                string? p = Safe(() => CheckCross(cipher, cipher, c));
                if (p != null)
                {
                    problems.Add(at + ": " + p);
                }
                p = Safe(() => CheckTamper(cipher, cipher, c));
                if (p != null)
                {
                    problems.Add(at + ": " + p);
                }
            }

            // 태그보다 짧은 암호문은 예외 없이 false 이고 출력(빈 버퍼)을 건드리지 않는다
            for (int len = 0; len < AeadSizes.TagBytes; len++)
            {
                string at = name + " short input " + len.ToString(CultureInfo.InvariantCulture);
                try
                {
                    byte[] output = Sentinel(4);
                    byte[] before = (byte[])output.Clone();
                    if (cipher.Open(key, nonce, aad, Pattern(len, 1), output.AsSpan(0, 0)))
                    {
                        problems.Add(at + ": Open returned true");
                    }
                    Same(problems, at + " output untouched", before, output);
                }
                catch (Exception ex)
                {
                    problems.Add(at + ": threw " + ex.GetType().FullName + ": " + ex.Message);
                }
            }

            // 인자 오류는 ArgumentException(호출자의 잘못. CONTRACTS 0.2)
            byte[] pt = Pattern(20, 2);
            ExpectArgument(problems, name + " seal key 31", () => cipher.Seal(new byte[31], nonce, aad, pt, new byte[36]));
            ExpectArgument(problems, name + " seal key 33", () => cipher.Seal(new byte[33], nonce, aad, pt, new byte[36]));
            ExpectArgument(problems, name + " seal nonce 8", () => cipher.Seal(key, new byte[8], aad, pt, new byte[36]));
            ExpectArgument(problems, name + " seal nonce 24", () => cipher.Seal(key, new byte[24], aad, pt, new byte[36]));
            ExpectArgument(problems, name + " seal output short", () => cipher.Seal(key, nonce, aad, pt, new byte[35]));
            ExpectArgument(problems, name + " seal output long", () => cipher.Seal(key, nonce, aad, pt, new byte[37]));
            ExpectArgument(problems, name + " open key 31", () => cipher.Open(new byte[31], nonce, aad, new byte[36], new byte[20]));
            ExpectArgument(problems, name + " open nonce 11", () => cipher.Open(key, new byte[11], aad, new byte[36], new byte[20]));
            ExpectArgument(problems, name + " open output short", () => cipher.Open(key, nonce, aad, new byte[36], new byte[19]));
            ExpectArgument(problems, name + " open output long", () => cipher.Open(key, nonce, aad, new byte[36], new byte[21]));

            try
            {
                CheckInPlace(problems, name, cipher, key, nonce, aad, pt);
            }
            catch (Exception ex)
            {
                problems.Add(name + " in place: threw " + ex.GetType().FullName + ": " + ex.Message);
            }
            return problems;
        }

        // 교차 성질: sealer 로 봉인한 것을 opener 가 열어 원래 평문을 얻고, 두 구현의 봉인 결과가 바이트 단위로 같다.
        public static string? CheckCross(IAeadCipher sealer, IAeadCipher opener, AeadCase c)
        {
            int n = c.Plaintext.Length;
            var a = new byte[n + AeadSizes.TagBytes];
            sealer.Seal(c.Key, c.Nonce, c.Aad, c.Plaintext, a);
            var b = new byte[n + AeadSizes.TagBytes];
            opener.Seal(c.Key, c.Nonce, c.Aad, c.Plaintext, b);
            if (!a.AsSpan().SequenceEqual(b))
            {
                return "the two ciphers sealed different bytes: " + AeadHex.Format(a) + " vs " + AeadHex.Format(b);
            }
            byte[] opened = Sentinel(n);
            if (!opener.Open(c.Key, c.Nonce, c.Aad, a, opened))
            {
                return "Open returned false for a ciphertext the other cipher sealed";
            }
            if (!opened.AsSpan().SequenceEqual(c.Plaintext))
            {
                return "Open returned a different plaintext: " + AeadHex.Format(opened);
            }
            byte[] again = Sentinel(n);
            if (!sealer.Open(c.Key, c.Nonce, c.Aad, b, again) || !again.AsSpan().SequenceEqual(c.Plaintext))
            {
                return "the sealer could not open the opener's ciphertext";
            }
            return null;
        }

        // 위조 성질: 암호문 ‖ 태그, AAD, nonce, 키 가운데 한 자리를 바꾸면 Open 은 false 이고 출력 버퍼는 한 바이트도 바뀌지 않는다.
        public static string? CheckTamper(IAeadCipher sealer, IAeadCipher opener, AeadCase c)
        {
            int n = c.Plaintext.Length;
            var sealedBytes = new byte[n + AeadSizes.TagBytes];
            sealer.Seal(c.Key, c.Nonce, c.Aad, c.Plaintext, sealedBytes);
            byte[] ct = sealedBytes;
            byte[] aad = c.Aad;
            byte[] nonce = c.Nonce;
            byte[] key = c.Key;
            int regions = sealedBytes.Length + c.Aad.Length + AeadSizes.NonceBytes + AeadSizes.KeyBytes;
            int at = c.FlipIndex % regions;
            string where;
            if (at < sealedBytes.Length)
            {
                ct = Flip(sealedBytes, at, c.FlipMask);
                where = (at < n ? "ciphertext[" : "tag[") + (at < n ? at : at - n).ToString(CultureInfo.InvariantCulture) + "]";
            }
            else if ((at -= sealedBytes.Length) < c.Aad.Length)
            {
                aad = Flip(c.Aad, at, c.FlipMask);
                where = "aad[" + at.ToString(CultureInfo.InvariantCulture) + "]";
            }
            else if ((at -= c.Aad.Length) < AeadSizes.NonceBytes)
            {
                nonce = Flip(c.Nonce, at, c.FlipMask);
                where = "nonce[" + at.ToString(CultureInfo.InvariantCulture) + "]";
            }
            else
            {
                at -= AeadSizes.NonceBytes;
                key = Flip(c.Key, at, c.FlipMask);
                where = "key[" + at.ToString(CultureInfo.InvariantCulture) + "]";
            }
            byte[] output = Sentinel(n);
            if (opener.Open(key, nonce, aad, ct, output))
            {
                return "Open returned true after changing " + where;
            }
            byte[] expected = Sentinel(n);
            if (!output.AsSpan().SequenceEqual(expected))
            {
                return "Open returned false but wrote into the output buffer (changed " + where + ")";
            }
            return null;
        }

        // 입력과 출력이 같은 버퍼여도 된다(AeadGuard 규칙): 패킷 버퍼 안에서 머리말(AAD)과 본문을 제자리로 봉인하고 연다.
        private static void CheckInPlace(List<string> problems, string name, IAeadCipher cipher, byte[] key, byte[] nonce, byte[] aad, byte[] pt)
        {
            var expected = new byte[pt.Length + AeadSizes.TagBytes];
            cipher.Seal(key, nonce, aad, pt, expected);

            var packet = new byte[aad.Length + pt.Length + AeadSizes.TagBytes];
            Array.Copy(aad, 0, packet, 0, aad.Length);
            Array.Copy(pt, 0, packet, aad.Length, pt.Length);
            Span<byte> body = packet.AsSpan(aad.Length);
            cipher.Seal(key, nonce, packet.AsSpan(0, aad.Length), body.Slice(0, pt.Length), body);
            Same(problems, name + " in-place seal", expected, body.ToArray());

            if (!cipher.Open(key, nonce, packet.AsSpan(0, aad.Length), body, body.Slice(0, pt.Length)))
            {
                problems.Add(name + " in-place open: returned false");
                return;
            }
            Same(problems, name + " in-place open", pt, body.Slice(0, pt.Length).ToArray());

            // 실패한 제자리 열기는 버퍼를 그대로 둔다
            cipher.Seal(key, nonce, aad, pt, body);
            body[0] ^= 0x01;
            byte[] before = packet.AsSpan().ToArray();
            if (cipher.Open(key, nonce, packet.AsSpan(0, aad.Length), body, body.Slice(0, pt.Length)))
            {
                problems.Add(name + " in-place open of a changed packet: returned true");
            }
            Same(problems, name + " in-place failed open leaves the buffer", before, packet);
        }

        private static void CheckRejected(List<string> problems, string at, IAeadCipher cipher, byte[] key, byte[] nonce, byte[] aad, byte[] ct)
        {
            int n = ct.Length >= AeadSizes.TagBytes ? ct.Length - AeadSizes.TagBytes : 0;
            byte[] output = Sentinel(n);
            if (cipher.Open(key, nonce, aad, ct, output))
            {
                problems.Add(at + ": Open returned true for a changed input");
            }
            Same(problems, at + " output untouched", Sentinel(n), output);
        }

        private static void ExpectArgument(List<string> problems, string at, Action call)
        {
            try
            {
                call();
                problems.Add(at + ": expected ArgumentException, nothing was thrown");
            }
            catch (ArgumentException)
            {
                // 기대한 결과
            }
            catch (Exception ex)
            {
                problems.Add(at + ": expected ArgumentException, got " + ex.GetType().FullName);
            }
        }

        private static string? Safe(Func<string?> check)
        {
            try
            {
                return check();
            }
            catch (Exception ex)
            {
                return "threw " + ex.GetType().FullName + ": " + ex.Message;
            }
        }

        private static void AddFuzz(List<string> problems, string what, FuzzResult<AeadCase> result)
        {
            if (!result.Passed)
            {
                problems.Add(what + ":\n" + result.Report);
            }
        }

        private static void Same(List<string> problems, string at, byte[] expected, byte[] actual)
        {
            if (!expected.AsSpan().SequenceEqual(actual))
            {
                problems.Add(at + ": expected " + AeadHex.Format(expected) + " got " + AeadHex.Format(actual));
            }
        }

        // 출력 버퍼의 처음 내용. 0 이 아닌 무늬라 "0으로 지웠다"도 잡힌다
        public static byte[] Sentinel(int length)
        {
            var b = new byte[length];
            for (int i = 0; i < length; i++)
            {
                b[i] = (byte)(0xA5 ^ (i * 37));
            }
            return b;
        }

        private static byte[] Pattern(int length, int step)
        {
            var b = new byte[length];
            for (int i = 0; i < length; i++)
            {
                b[i] = (byte)(i * step + 1);
            }
            return b;
        }

        private static byte[] Flip(byte[] source, int index, byte mask)
        {
            var b = (byte[])source.Clone();
            b[index] ^= mask;
            return b;
        }

        private static byte[] Append(byte[] source, byte value)
        {
            var b = new byte[source.Length + 1];
            Array.Copy(source, 0, b, 0, source.Length);
            b[source.Length] = value;
            return b;
        }

        private static byte[] Slice(byte[] source, int start, int length)
        {
            var b = new byte[length];
            Array.Copy(source, start, b, 0, length);
            return b;
        }
    }
}
