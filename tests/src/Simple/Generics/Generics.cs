// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Reflection;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

class Program
{
    static int Main()
    {
        TestDictionaryDependencyTracking.Run();
        TestStaticBaseLookups.Run();
        TestInitThisClass.Run();
        TestDelegateFatFunctionPointers.Run();
        TestDelegateToCanonMethods.Run();
        TestVirtualMethodUseTracking.Run();
        TestSlotsInHierarchy.Run();
        TestReflectionInvoke.Run();
        TestDelegateVirtualMethod.Run();
        TestDelegateInterfaceMethod.Run();
        TestThreadStaticFieldAccess.Run();
        TestConstrainedMethodCalls.Run();
        TestInstantiatingUnboxingStubs.Run();
        TestMDArrayAddressMethod.Run();
        TestNameManglingCollisionRegression.Run();
        TestSimpleGVMScenarios.Run();
        TestGvmDependencies.Run();
        TestFieldAccess.Run();

        return 100;
    }

    /// <summary>
    /// Tests that we properly track dictionary dependencies of generic methods.
    /// (Getting this wrong is a linker failure.)
    /// </summary>
    class TestDictionaryDependencyTracking
    {
        static object Gen1<T>()
        {
            return MakeArray<ClassGen<T>>();
        }

        static object MakeArray<T>()
        {
            return new T[0];
        }

        class Gen<T>
        {
            public object Frob()
            {
                return new ValueGen<T[]>();
            }

            public object Futz()
            {
                return Gen1<ValueGen<T>>();
            }
        }

        struct ValueGen<T>
        {
        }

        class ClassGen<T>
        {
        }

        public static void Run()
        {
            new Gen<string>().Frob();
            new Gen<object>().Futz();
        }
    }

    /// <summary>
    /// Tests static base access.
    /// </summary>
    class TestStaticBaseLookups
    {
        class C1 { }
        class C2 { }
        class C3 { }

        class GenHolder<T>
        {
            public static int IntField;
            public static string StringField;
        }

        class GenAccessor<T>
        {
            public static string Read()
            {
                return GenHolder<T>.IntField.ToString() + GenHolder<T>.StringField;
            }

            public static void SetSimple(int i, string s)
            {
                GenHolder<T>.IntField = i;
                GenHolder<T>.StringField = s;
            }

            public static void SetComplex<U>(int i, string s)
            {
                GenHolder<T>.IntField = i;
                GenHolder<T>.StringField = s;
                GenHolder<U>.IntField = i + 1;
                GenHolder<U>.StringField = s + "`";
            }
        }

        public static void Run()
        {
            GenAccessor<C1>.SetComplex<C2>(42, "Hello");
            GenAccessor<C3>.SetSimple(85, "World");

            if (GenAccessor<C1>.Read() != "42Hello")
                throw new Exception();

            if (GenHolder<C2>.IntField != 43 || GenHolder<C2>.StringField != "Hello`")
                throw new Exception();

            if (GenAccessor<C3>.Read() != "85World")
                throw new Exception();
        }
    }

    /// <summary>
    /// Tests that we can use a delegate that points to a generic method.
    /// </summary>
    class TestDelegateFatFunctionPointers
    {
        struct SmallStruct
        {
            public int X;
        }

        struct MediumStruct
        {
            public int X, Y, Z, W;
        }

        unsafe struct BigStruct
        {
            public const int Length = 128;
            public fixed byte Bytes[Length];
        }

        T Generic<T>(object o) where T : class
        {
            Func<object, T> f = OtherGeneric<T>;
            return f(o);
        }

        T OtherGeneric<T>(object o) where T : class
        {
            return o as T;
        }

        delegate void VoidGenericDelegate<T>(ref T x, T val);
        void VoidGeneric<T>(ref T x, T val)
        {
            x = val;
        }

        SmallStruct SmallStructGeneric<T>(SmallStruct x)
        {
            return x;
        }

        MediumStruct MediumStructGeneric<T>(MediumStruct x)
        {
            return x;
        }

        BigStruct BigStructGeneric<T>(BigStruct x)
        {
            return x;
        }

        public static void Run()
        {
            var o = new TestDelegateFatFunctionPointers();

            string hw = "Hello World";
            string roundtrip = o.Generic<string>(hw);
            if (roundtrip != hw)
                throw new Exception();

            {
                VoidGenericDelegate<object> f = o.VoidGeneric;
                object obj = new object();
                object location = null;
                f(ref location, obj);
                if (location != obj)
                    throw new Exception();
            }

            {
                Func<SmallStruct, SmallStruct> f = o.SmallStructGeneric<object>;
                SmallStruct x = new SmallStruct { X = 12345 };
                SmallStruct result = f(x);
                if (result.X != x.X)
                    throw new Exception();
            }

            {
                Func<MediumStruct, MediumStruct> f = o.MediumStructGeneric<object>;
                MediumStruct x = new MediumStruct { X = 12, Y = 34, Z = 56, W = 78 };
                MediumStruct result = f(x);
                if (result.X != x.X || result.Y != x.Y || result.Z != x.Z || result.W != x.W)
                    throw new Exception();
            }

            unsafe
            {
                Func<BigStruct, BigStruct> f = o.BigStructGeneric<object>;
                BigStruct x = new BigStruct();
                for (int i = 0; i < BigStruct.Length; i++)
                    x.Bytes[i] = (byte)(i * 2);

                BigStruct result = f(x);

                for (int i = 0; i < BigStruct.Length; i++)
                    if (x.Bytes[i] != result.Bytes[i])
                        throw new Exception();
            }
        }
    }

    class TestDelegateToCanonMethods
    {
        class Foo
        {
            public readonly int Value;
            public Foo(int value)
            {
                Value = value;
            }

            public override string ToString()
            {
                return Value.ToString();
            }
        }

        class Bar
        {
            public readonly int Value;
            public Bar(int value)
            {
                Value = value;
            }

            public override string ToString()
            {
                return Value.ToString();
            }
        }

        class GenClass<T>
        {
            public readonly T X;

            public GenClass(T x)
            {
                X = x;
            }

            public string MakeString()
            {
                // Use a constructed type that is not used elsewhere
                return typeof(T[,]).GetElementType().Name + ": " + X.ToString();
            }

            public string MakeGenString<U>()
            {
                // Use a constructed type that is not used elsewhere
                return typeof(T[,,]).GetElementType().Name + ", " + 
                    typeof(U[,,,]).GetElementType().Name + ": " + X.ToString();
            }
        }

        struct GenStruct<T>
        {
            public readonly T X;

            public GenStruct(T x)
            {
                X = x;
            }

            public string MakeString()
            {
                // Use a constructed type that is not used elsewhere
                return typeof(T[,]).GetElementType().Name + ": " + X.ToString();
            }

            public string MakeGenString<U>()
            {
                // Use a constructed type that is not used elsewhere
                return typeof(T[,,]).GetElementType().Name + ", " +
                    typeof(U[,,,]).GetElementType().Name + ": " + X.ToString();
            }
        }

        public static void Run()
        {
            // Delegate to a shared nongeneric reference type instance method
            {
                GenClass<Foo> g = new GenClass<Foo>(new Foo(42));
                Func<string> f = g.MakeString;
                if (f() != "Foo: 42")
                    throw new Exception();
            }

            // Delegate to a unshared nongeneric reference type instance method
            {
                GenClass<int> g = new GenClass<int>(85);
                Func<string> f = g.MakeString;
                if (f() != "Int32: 85")
                    throw new Exception();
            }

            // Delegate to a shared generic reference type instance method
            {
                GenClass<Foo> g = new GenClass<Foo>(new Foo(42));
                Func<string> f = g.MakeGenString<Foo>;
                if (f() != "Foo, Foo: 42")
                    throw new Exception();
            }

            // Delegate to a unshared generic reference type instance method
            {
                GenClass<int> g = new GenClass<int>(85);
                Func<string> f = g.MakeGenString<int>;
                if (f() != "Int32, Int32: 85")
                    throw new Exception();
            }

            // Delegate to a shared nongeneric value type instance method
            /*{
                GenStruct<Bar> g = new GenStruct<Bar>(new Bar(42));
                Func<string> f = g.MakeString;
                if (f() != "Bar: 42")
                    throw new Exception();
            }*/

            // Delegate to a unshared nongeneric value type instance method
            {
                GenStruct<int> g = new GenStruct<int>(85);
                Func<string> f = g.MakeString;
                if (f() != "Int32: 85")
                    throw new Exception();
            }

            // Delegate to a shared generic value type instance method
            /*{
                GenStruct<Bar> g = new GenStruct<Bar>(new Bar(42));
                Func<string> f = g.MakeGenString<Bar>;
                if (f() != "Bar, Bar: 42")
                    throw new Exception();
            }*/

            // Delegate to a unshared generic value type instance method
            {
                GenStruct<int> g = new GenStruct<int>(85);
                Func<string> f = g.MakeGenString<int>;
                if (f() != "Int32, Int32: 85")
                    throw new Exception();
            }

        }
    }

    class TestDelegateVirtualMethod
    {
        static void Generic<T>()
        {
            Base<T> o = new Derived<T>();
            Func<string> f = o.Do;
            if (f() != "Derived")
                throw new Exception();

            o = new Base<T>();
            f = o.Do;
            if (f() != "Base")
                throw new Exception();
        }

        public static void Run()
        {
            Generic<string>();
        }

        class Base<T>
        {
            public virtual string Do() => "Base";
        }

        class Derived<T> : Base<T>
        {
            public override string Do() => "Derived";
        }
    }

    class TestDelegateInterfaceMethod
    {
        static void Generic<T>()
        {
            IFoo<T> o = new Foo<T>();
            Func<string> f = o.Do;
            if (f() != "Foo")
                throw new Exception();
        }

        public static void Run()
        {
            Generic<string>();
        }

        interface IFoo<T>
        {
            string Do();
        }

        class Foo<T> : IFoo<T>
        {
            public string Do() => "Foo";
        }
    }

    /// <summary>
    /// Tests RyuJIT's initThisClass.
    /// </summary>
    class TestInitThisClass
    {
        class Gen1<T> where T : class
        {
            static string s_str1;
            static string s_str2;

            static Gen1()
            {
                s_str1 = ("Hello" as T) as string;
                s_str2 = ("World" as T) as string;
            }

            public static string Get1()
            {
                return (s_str1 as T) as string;
            }

            public static string Get2<U>()
            {
                return (s_str2 as T) as string;
            }
        }

        class Gen2<T> where T : class
        {
            public static string GetFromClassParam()
            {
                return (Gen1<T>.Get1() as T) as string;
            }

            public static string GetFromMethodParam()
            {
                return (Gen1<T>.Get2<T>() as T) as string;
            }
        }

        class NonGeneric
        {
            public static readonly string Message;

            static NonGeneric()
            {
                Message = "Hi there";
            }

            public static string Get<T>(object o)
            {
                if (o is T[])
                    return Message;
                return null;
            }
        }

        public static void Run()
        {
            if (Gen2<string>.GetFromClassParam() != "Hello")
                throw new Exception();

            if (Gen2<string>.GetFromMethodParam() != "World")
                throw new Exception();

            if (NonGeneric.Get<object>(new object[0]) != "Hi there")
                throw new Exception();
        }
    }

    /// <summary>
    /// Tests that lazily built vtables for canonically equivalent types have the same shape.
    /// </summary>
    class TestVirtualMethodUseTracking
    {
        class C1 { }
        class C2 { }

        class Base1<T> where T : class
        {
            public virtual T As(object o)
            {
                return o as T;
            }
        }

        class Derived1<T> : Base1<T> where T : class
        {
            public T AsToo(object o)
            {
                return o as T;
            }
        }

        class Base2<T>
        {
            public virtual string Method1() => "Base2.Method1";
            public virtual string Method2() => "Base2.Method2";
        }

        class Derived2<T> : Base2<T>
        {
            public override string Method1() => "Derived2.Method1";
            public override string Method2() => "Derived2.Method2";
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static string TestMethod1FromSharedCode<T>(Base2<T> o) => o.Method1();

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static string TestMethod2FromSharedCode<T>(Base2<T> o) => o.Method2();

        public static void Run()
        {
            C1 c1 = new C1();
            if (new Derived1<C1>().As(c1) != c1)
                throw new Exception();

            C2 c2 = new C2();
            if (new Derived1<C2>().AsToo(c2) != c2)
                throw new Exception();

            // Also test the stability of the vtables.
            Base2<string> b1 = new Derived2<string>();
            if (b1.Method1() != "Derived2.Method1")
                throw new Exception();
            Base2<object> b2 = new Derived2<object>();
            if (b2.Method2() != "Derived2.Method2")
                throw new Exception();
            if (TestMethod1FromSharedCode(b2) != "Derived2.Method1")
                throw new Exception();
            if (TestMethod1FromSharedCode(b1) != "Derived2.Method1")
                throw new Exception();
            if (TestMethod2FromSharedCode(b2) != "Derived2.Method2")
                throw new Exception();
            if (TestMethod2FromSharedCode(b1) != "Derived2.Method2")
                throw new Exception();
        }
    }

    /// <summary>
    /// Makes sure that during the base slot computation for types such as
    /// Derived&lt;__Canon&gt; (where the base type ends up being Base&lt;__Canon, string&gt;),
    /// the lazy vtable slot computation works.
    /// </summary>
    class TestSlotsInHierarchy
    {
        class Base<T, U>
        {
            public virtual int Do()
            {
                return 42;
            }
        }

        class Derived<T> : Base<T, string> where T : class
        {
            public T Cast(object v)
            {
                return v as T;
            }
        }

        public static void Run()
        {
            var derived = new Derived<string>();
            var derivedAsBase = (Base<string, string>)derived;

            if (derivedAsBase.Do() != 42)
                throw new Exception();

            if (derived.Cast("Hello") != "Hello")
                throw new Exception();
        }
    }

    class TestReflectionInvoke
    {
        static int s_NumErrors = 0;

        struct Foo<T>
        {
            public int Value;

            [MethodImpl(MethodImplOptions.NoInlining)]
            public bool SetAndCheck<U>(int value, U check)
            {
                Value = value;
                return check != null && typeof(T) == typeof(U);
            }
        }

        public interface IFace<T>
        {
            string IFaceMethod1(T t);
            string IFaceGVMethod1<U>(T t, U u);
        }

        public class BaseClass<T> : IFace<T>
        {
            public virtual string Method1(T t) { return "BaseClass.Method1"; }
            public virtual string Method2(T t) { return "BaseClass.Method2"; }
            public virtual string Method3(T t) { return "BaseClass.Method3"; }
            public virtual string Method4(T t) { return "BaseClass.Method4"; }
            public virtual string GVMethod1<U>(T t, U u) { return "BaseClass.GVMethod1"; }
            public virtual string GVMethod2<U>(T t, U u) { return "BaseClass.GVMethod2"; }
            public virtual string GVMethod3<U>(T t, U u) { return "BaseClass.GVMethod3"; }
            public virtual string GVMethod4<U>(T t, U u) { return "BaseClass.GVMethod4"; }

            public virtual string IFaceMethod1(T t) { return "BaseClass.IFaceMethod1"; }
            public virtual string IFaceGVMethod1<U>(T t, U u) { return "BaseClass.IFaceGVMethod1"; }
        }

        public class DerivedClass1<T> : BaseClass<T>, IFace<T>
        {
            public override sealed string Method1(T t) { return "DerivedClass1.Method1"; }
            public override string Method2(T t) { return "DerivedClass1.Method2"; }
            public new virtual string Method3(T t) { return "DerivedClass1.Method3"; }
            public override sealed string GVMethod1<U>(T t, U u) { return "DerivedClass1.GVMethod1"; }
            public override string GVMethod2<U>(T t, U u) { return "DerivedClass1.GVMethod2"; }
            public new virtual string GVMethod3<U>(T t, U u) { return "DerivedClass1.GVMethod3"; }

            public override string IFaceMethod1(T t) { return "DerivedClass1.IFaceMethod1"; }
        }

        public class DerivedClass2<T> : DerivedClass1<T>, IFace<T>
        {
            public override string Method3(T t) { return "DerivedClass2.Method3"; }
            public override string Method4(T t) { return "DerivedClass2.Method4"; }
            public override string GVMethod3<U>(T t, U u) { return "DerivedClass2.GVMethod3"; }
            public override string GVMethod4<U>(T t, U u) { return "DerivedClass2.GVMethod4"; }

            string IFace<T>.IFaceMethod1(T t) { return "DerivedClass2.IFaceMethod1"; }
            public override string IFaceGVMethod1<U>(T t, U u) { return "DerivedClass2.IFaceGVMethod1"; }
        }

        private static void Verify<T>(T expected, T actual)
        {
            if (!actual.Equals(expected))
            {
                Console.WriteLine("ACTUAL   : " + actual);
                Console.WriteLine("EXPECTED : " + expected);
                s_NumErrors++;
            }
        }

        public static void Run()
        {
            if (String.Empty.Length > 0)
            {
                // Make sure we compile this method body.
                var tmp = new Foo<string>();
                tmp.SetAndCheck<string>(0, null);
            }

            object o = new Foo<string>();

            {
                MethodInfo mi = typeof(Foo<string>).GetTypeInfo().GetDeclaredMethod("SetAndCheck").MakeGenericMethod(typeof(string));
                if (!(bool)mi.Invoke(o, new object[] { 123, "hello" }))
                    s_NumErrors++;

                var foo = (Foo<string>)o;
                if (foo.Value != 123)
                    s_NumErrors++;

                if ((bool)mi.Invoke(o, new object[] { 123, null }))
                    s_NumErrors++;
            }

            // Uncomment when we have the type loader to buld invoke stub dictionaries.
            /*{
                MethodInfo mi = typeof(Foo<string>).GetTypeInfo().GetDeclaredMethod("SetAndCheck").MakeGenericMethod(typeof(object));
                if ((bool)mi.Invoke(o, new object[] { 123, new object() }))
                    s_NumErrors++;
            }*/

            // VirtualInvokeMap testing
            {
                // Rooting some methods to make them reflectable
                new BaseClass<string>().Method1("string");
                new BaseClass<string>().Method2("string");
                new BaseClass<string>().Method3("string");
                new BaseClass<string>().Method4("string");
                new BaseClass<string>().GVMethod1<string>("string", "string2");
                new BaseClass<string>().GVMethod2<string>("string", "string2");
                new BaseClass<string>().GVMethod3<string>("string", "string2");
                new BaseClass<string>().GVMethod4<string>("string", "string2");
                new DerivedClass1<string>().Method1("string");
                new DerivedClass1<string>().Method2("string");
                new DerivedClass1<string>().Method3("string");
                new DerivedClass1<string>().Method4("string");
                new DerivedClass1<string>().GVMethod1<string>("string", "string2");
                new DerivedClass1<string>().GVMethod2<string>("string", "string2");
                new DerivedClass1<string>().GVMethod3<string>("string", "string2");
                new DerivedClass1<string>().GVMethod4<string>("string", "string2");
                new DerivedClass2<string>().Method1("string");
                new DerivedClass2<string>().Method2("string");
                new DerivedClass2<string>().Method3("string");
                new DerivedClass2<string>().Method4("string");
                new DerivedClass2<string>().GVMethod1<string>("string", "string2");
                new DerivedClass2<string>().GVMethod2<string>("string", "string2");
                new DerivedClass2<string>().GVMethod3<string>("string", "string2");
                new DerivedClass2<string>().GVMethod4<string>("string", "string2");
                ((IFace<string>)new BaseClass<string>()).IFaceMethod1("string");
                ((IFace<string>)new BaseClass<string>()).IFaceGVMethod1<string>("string1", "string2");

                MethodInfo m1 = typeof(BaseClass<string>).GetTypeInfo().GetDeclaredMethod("Method1");
                MethodInfo m2 = typeof(BaseClass<string>).GetTypeInfo().GetDeclaredMethod("Method2");
                MethodInfo m3 = typeof(BaseClass<string>).GetTypeInfo().GetDeclaredMethod("Method3");
                MethodInfo m4 = typeof(BaseClass<string>).GetTypeInfo().GetDeclaredMethod("Method4");
                MethodInfo gvm1 = typeof(BaseClass<string>).GetTypeInfo().GetDeclaredMethod("GVMethod1").MakeGenericMethod(typeof(string));
                MethodInfo gvm2 = typeof(BaseClass<string>).GetTypeInfo().GetDeclaredMethod("GVMethod2").MakeGenericMethod(typeof(string));
                MethodInfo gvm3 = typeof(BaseClass<string>).GetTypeInfo().GetDeclaredMethod("GVMethod3").MakeGenericMethod(typeof(string));
                MethodInfo gvm4 = typeof(BaseClass<string>).GetTypeInfo().GetDeclaredMethod("GVMethod4").MakeGenericMethod(typeof(string));
                Verify("BaseClass.Method1", m1.Invoke(new BaseClass<string>(), new[] { "" }));
                Verify("BaseClass.Method2", m2.Invoke(new BaseClass<string>(), new[] { "" }));
                Verify("BaseClass.Method3", m3.Invoke(new BaseClass<string>(), new[] { "" }));
                Verify("BaseClass.Method4", m4.Invoke(new BaseClass<string>(), new[] { "" }));
                Verify("DerivedClass1.Method1", m1.Invoke(new DerivedClass1<string>(), new[] { "" }));
                Verify("DerivedClass1.Method2", m2.Invoke(new DerivedClass1<string>(), new[] { "" }));
                Verify("BaseClass.Method3", m3.Invoke(new DerivedClass1<string>(), new[] { "" }));
                Verify("BaseClass.Method4", m4.Invoke(new DerivedClass1<string>(), new[] { "" }));
                Verify("DerivedClass1.Method1", m1.Invoke(new DerivedClass2<string>(), new[] { "" }));
                Verify("DerivedClass1.Method2", m2.Invoke(new DerivedClass2<string>(), new[] { "" }));
                Verify("BaseClass.Method3", m3.Invoke(new DerivedClass2<string>(), new[] { "" }));
                Verify("DerivedClass2.Method4", m4.Invoke(new DerivedClass2<string>(), new[] { "" }));
                Verify("BaseClass.GVMethod1", gvm1.Invoke(new BaseClass<string>(), new[] { "", "" }));
                Verify("BaseClass.GVMethod2", gvm2.Invoke(new BaseClass<string>(), new[] { "", "" }));
                Verify("BaseClass.GVMethod3", gvm3.Invoke(new BaseClass<string>(), new[] { "", "" }));
                Verify("BaseClass.GVMethod4", gvm4.Invoke(new BaseClass<string>(), new[] { "", "" }));
                Verify("DerivedClass1.GVMethod1", gvm1.Invoke(new DerivedClass1<string>(), new[] { "", "" }));
                Verify("DerivedClass1.GVMethod2", gvm2.Invoke(new DerivedClass1<string>(), new[] { "", "" }));
                Verify("BaseClass.GVMethod3", gvm3.Invoke(new DerivedClass1<string>(), new[] { "", "" }));
                Verify("BaseClass.GVMethod4", gvm4.Invoke(new DerivedClass1<string>(), new[] { "", "" }));
                Verify("DerivedClass1.GVMethod1", gvm1.Invoke(new DerivedClass2<string>(), new[] { "", "" }));
                Verify("DerivedClass1.GVMethod2", gvm2.Invoke(new DerivedClass2<string>(), new[] { "", "" }));
                Verify("BaseClass.GVMethod3", gvm3.Invoke(new DerivedClass2<string>(), new[] { "", "" }));
                Verify("DerivedClass2.GVMethod4", gvm4.Invoke(new DerivedClass2<string>(), new[] { "", "" }));

                m1 = typeof(DerivedClass1<string>).GetTypeInfo().GetDeclaredMethod("Method1");
                m2 = typeof(DerivedClass1<string>).GetTypeInfo().GetDeclaredMethod("Method2");
                m3 = typeof(DerivedClass1<string>).GetTypeInfo().GetDeclaredMethod("Method3");
                gvm1 = typeof(DerivedClass1<string>).GetTypeInfo().GetDeclaredMethod("GVMethod1").MakeGenericMethod(typeof(string));
                gvm2 = typeof(DerivedClass1<string>).GetTypeInfo().GetDeclaredMethod("GVMethod2").MakeGenericMethod(typeof(string));
                gvm3 = typeof(DerivedClass1<string>).GetTypeInfo().GetDeclaredMethod("GVMethod3").MakeGenericMethod(typeof(string));
                Verify("DerivedClass1.Method1", m1.Invoke(new DerivedClass1<string>(), new[] { "" }));
                Verify("DerivedClass1.Method2", m2.Invoke(new DerivedClass1<string>(), new[] { "" }));
                Verify("DerivedClass1.Method3", m3.Invoke(new DerivedClass1<string>(), new[] { "" }));
                Verify("DerivedClass1.Method1", m1.Invoke(new DerivedClass2<string>(), new[] { "" }));
                Verify("DerivedClass1.Method2", m2.Invoke(new DerivedClass2<string>(), new[] { "" }));
                Verify("DerivedClass2.Method3", m3.Invoke(new DerivedClass2<string>(), new[] { "" }));
                Verify("DerivedClass1.GVMethod1", gvm1.Invoke(new DerivedClass1<string>(), new[] { "", "" }));
                Verify("DerivedClass1.GVMethod2", gvm2.Invoke(new DerivedClass1<string>(), new[] { "", "" }));
                Verify("DerivedClass1.GVMethod3", gvm3.Invoke(new DerivedClass1<string>(), new[] { "", "" }));
                Verify("DerivedClass1.GVMethod1", gvm1.Invoke(new DerivedClass2<string>(), new[] { "", "" }));
                Verify("DerivedClass1.GVMethod2", gvm2.Invoke(new DerivedClass2<string>(), new[] { "", "" }));
                Verify("DerivedClass2.GVMethod3", gvm3.Invoke(new DerivedClass2<string>(), new[] { "", "" }));

                m3 = typeof(DerivedClass2<string>).GetTypeInfo().GetDeclaredMethod("Method3");
                m4 = typeof(DerivedClass2<string>).GetTypeInfo().GetDeclaredMethod("Method4");
                gvm3 = typeof(DerivedClass2<string>).GetTypeInfo().GetDeclaredMethod("GVMethod3").MakeGenericMethod(typeof(string));
                gvm4 = typeof(DerivedClass2<string>).GetTypeInfo().GetDeclaredMethod("GVMethod4").MakeGenericMethod(typeof(string));
                Verify("DerivedClass2.Method3", m3.Invoke(new DerivedClass2<string>(), new[] { "" }));
                Verify("DerivedClass2.Method4", m4.Invoke(new DerivedClass2<string>(), new[] { "" }));
                Verify("DerivedClass2.GVMethod3", gvm3.Invoke(new DerivedClass2<string>(), new[] { "", "" }));
                Verify("DerivedClass2.GVMethod4", gvm4.Invoke(new DerivedClass2<string>(), new[] { "", "" }));

                // BaseClass<int>.Method1 has the same slot as BaseClass<float>.Method3 on CoreRT, because vtable entries
                // get populated on demand (the first type won't get a Method3 entry, and the latter won't get a Method1 entry)
                // On ProjectN, both types will get vtable entries for both methods.
                new BaseClass<int>().Method1(1);
                m1 = typeof(BaseClass<int>).GetTypeInfo().GetDeclaredMethod("Method1");
                Verify("BaseClass.Method1", m1.Invoke(new BaseClass<int>(), new object[] { (int)1 }));
                Verify("DerivedClass1.Method1", m1.Invoke(new DerivedClass1<int>(), new object[] { (int)1 }));
                Verify("DerivedClass1.Method1", m1.Invoke(new DerivedClass2<int>(), new object[] { (int)1 }));

                new BaseClass<float>().Method3(1);
                m3 = typeof(BaseClass<float>).GetTypeInfo().GetDeclaredMethod("Method3");
                Verify("BaseClass.Method3", m3.Invoke(new BaseClass<float>(), new object[] { 1.1f }));
                Verify("BaseClass.Method3", m3.Invoke(new DerivedClass1<float>(), new object[] { 1.1f }));
                Verify("BaseClass.Method3", m3.Invoke(new DerivedClass2<float>(), new object[] { 1.1f }));

                m1 = typeof(IFace<string>).GetTypeInfo().GetDeclaredMethod("IFaceMethod1");
                gvm1 = typeof(IFace<string>).GetTypeInfo().GetDeclaredMethod("IFaceGVMethod1").MakeGenericMethod(typeof(string));
                Verify("BaseClass.IFaceMethod1", m1.Invoke(new BaseClass<string>(), new[] { "" }));
                Verify("BaseClass.IFaceGVMethod1", gvm1.Invoke(new BaseClass<string>(), new[] { "", "" }));
                Verify("DerivedClass1.IFaceMethod1", m1.Invoke(new DerivedClass1<string>(), new[] { "" }));
                Verify("BaseClass.IFaceGVMethod1", gvm1.Invoke(new DerivedClass1<string>(), new[] { "", "" }));
                Verify("DerivedClass2.IFaceMethod1", m1.Invoke(new DerivedClass2<string>(), new[] { "" }));
                Verify("DerivedClass2.IFaceGVMethod1", gvm1.Invoke(new DerivedClass2<string>(), new[] { "", "" }));
            }

            if (s_NumErrors != 0)
                throw new Exception();
        }
    }

    class TestThreadStaticFieldAccess
    {
        class TypeWithThreadStaticField<T>
        {
            [ThreadStatic]
            public static int X;

            [MethodImpl(MethodImplOptions.NoInlining)]
            public static int Read()
            {
                return X;
            }

            [MethodImpl(MethodImplOptions.NoInlining)]
            public static void Write(int x)
            {
                X = x;
            }
        }

        class BeforeFieldInitType<T>
        {
            [ThreadStatic]
            public static int X = 1985;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int ReadFromBeforeFieldInitType<T>()
        {
            return BeforeFieldInitType<T>.X;
        }

        public static void Run()
        {
            // This will set the field to a value from non-shared code
            TypeWithThreadStaticField<object>.X = 42;

            // Now read the value from shared code
            if (TypeWithThreadStaticField<object>.Read() != 42)
                throw new Exception();

            // Set the value from shared code
            TypeWithThreadStaticField<string>.Write(112);

            // Now read the value from non-shared code
            if (TypeWithThreadStaticField<string>.X != 112)
                throw new Exception();

            // Check that the storage locations for string and object instantiations differ
            if (TypeWithThreadStaticField<object>.Read() != 42)
                throw new Exception();

            // Make sure we run the cctor
            if (ReadFromBeforeFieldInitType<object>() != 1985)
                throw new Exception();
        }
    }

    class TestConstrainedMethodCalls
    {
        interface IFoo<T>
        {
            void Frob();
        }

        struct Foo<T> : IFoo<T>
        {
            public int FrobbedValue;

            public void Frob()
            {
                FrobbedValue = 12345;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static void DoFrob<T, U>(ref T t) where T : IFoo<U>
        {
            // Perform a constrained interface call from shared code.
            // This should have been resolved to a direct call at compile time.
            t.Frob();
        }

        public static void Run()
        {
            var foo = new Foo<object>();
            DoFrob<Foo<object>, object>(ref foo);

            // If the FrobbedValue doesn't change when we frob, we must have done box+interface call.
            if (foo.FrobbedValue != 12345)
                throw new Exception();
        }
    }

    class TestInstantiatingUnboxingStubs
    {
        static volatile IFoo s_foo;

        interface IFoo
        {
            bool IsInst(object o);

            void Set(int value);
        }

        struct Foo<T> : IFoo
        {
            public int Value;

            public bool IsInst(object o)
            {
                return o is T;
            }

            public void Set(int value)
            {
                Value = value;
            }
        }

        public static void Run()
        {
            s_foo = new Foo<string>();

            // Make sure the instantiation argument is properly passed
            if (!s_foo.IsInst("ab"))
                throw new Exception();

            if (s_foo.IsInst(new object()))
                throw new Exception();

            // Make sure the byref to 'this' is properly passed
            s_foo.Set(42);

            var foo = (Foo<string>)s_foo;
            if (foo.Value != 42)
                throw new Exception();
        }
    }

    class TestMDArrayAddressMethod
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void PassByRef(ref object x)
        {
            x = new Object();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void DoGen<T>(object[,] arr)
        {
            // Here, the array type is known statically at the time of compilation
            PassByRef(ref arr[0, 0]);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void PassByRef2<T>(ref T x)
        {
            x = default(T);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void DoGen2<T>(T[,] arr)
        {
            // Here, the array type needs to be looked up from the dictionary
            PassByRef2<T>(ref arr[0, 0]);
        }

        public static void Run()
        {
            int exceptionsSeen = 0;

            try
            {
                DoGen<object>(new string[1, 1]);
            }
            catch (ArrayTypeMismatchException)
            {
                exceptionsSeen++;
            }

            DoGen<object>(new object[1, 1]);

            try
            {
                DoGen2<object>(new string[1, 1]);
            }
            catch (ArrayTypeMismatchException)
            {
                exceptionsSeen++;
            }

            DoGen2<object>(new object[1, 1]);

            if (exceptionsSeen != 2)
                throw new Exception();
        }
    }

    //
    // Regression test for issue https://github.com/dotnet/corert/issues/1964
    //
    class TestNameManglingCollisionRegression
    {
        class Gen1<T>
        {
            public Gen1(T t) {}
        }

        public static void Run()
        {
            Gen1<object[]>[] g1 = new Gen1<object[]>[1];
            g1[0] = new Gen1<object[]>(new object[] {new object[1]});

            Gen1<object[][]> g2 = new Gen1<object[][]>(new object[1][]);
        }
    }

    class TestSimpleGVMScenarios
    {
        interface IFoo<out U>
        {
            string IMethod1<T>(T t1, T t2);
        }

        class Base : IFoo<string>, IFoo<int>
        {
            public virtual string GMethod1<T>(T t1, T t2) { return "Base.GMethod1<" + typeof(T) + ">(" + t1 + "," + t2 + ")"; }
            public virtual string IMethod1<T>(T t1, T t2) { return "Base.IMethod1<" + typeof(T) + ">(" + t1 + "," + t2 + ")"; }
        }
        class Derived : Base, IFoo<string>, IFoo<int>
        {
            public override string GMethod1<T>(T t1, T t2) { return "Derived.GMethod1<" + typeof(T) + ">(" + t1 + "," + t2 + ")"; }
            string IFoo<string>.IMethod1<T>(T t1, T t2) { return "Derived.IFoo<string>.IMethod1<" + typeof(T) + ">(" + t1 + "," + t2 + ")"; }
        }
        class SuperDerived : Derived, IFoo<string>, IFoo<int>
        {
            string IFoo<int>.IMethod1<T>(T t1, T t2) { return "SuperDerived.IFoo<int>.IMethod1<" + typeof(T) + ">(" + t1 + "," + t2 + ")"; }
        }
        

        class GenBase<A> : IFoo<string>, IFoo<int>
        {
            public virtual string GMethod1<T>(T t1, T t2) { return "GenBase<" + typeof(A) + ">.GMethod1<" + typeof(T) + ">(" + t1 + "," + t2 + ")"; }
            public virtual string IMethod1<T>(T t1, T t2) { return "GenBase<" + typeof(A) + ">.IMethod1<" + typeof(T) + ">(" + t1 + "," + t2 + ")"; }
        }
        class GenDerived<A> : GenBase<A>, IFoo<string>, IFoo<int>
        {
            public override string GMethod1<T>(T t1, T t2) { return "GenDerived<" + typeof(A) + ">.GMethod1<" + typeof(T) + ">(" + t1 + "," + t2 + ")"; }
            string IFoo<string>.IMethod1<T>(T t1, T t2) { return "GenDerived<" + typeof(A) + ">.IFoo<string>.IMethod1<" + typeof(T) + ">(" + t1 + "," + t2 + ")"; }
        }
        class GenSuperDerived<A> : GenDerived<A>, IFoo<string>, IFoo<int>
        {
            string IFoo<int>.IMethod1<T>(T t1, T t2) { return "GenSuperDerived<" + typeof(A) + ">.IFoo<int>.IMethod1<" + typeof(T) + ">(" + t1 + "," + t2 + ")"; }
        }

        struct MyStruct1 : IFoo<string>, IFoo<int>
        {
            string IFoo<string>.IMethod1<T>(T t1, T t2) { return "MyStruct1.IFoo<string>.IMethod1<" + typeof(T) + ">(" + t1 + "," + t2 + ")"; }
            string IFoo<int>.IMethod1<T>(T t1, T t2) { return "MyStruct1.IFoo<int>.IMethod1<" + typeof(T) + ">(" + t1 + "," + t2 + ")"; }
        }
        struct MyStruct2 : IFoo<string>, IFoo<int>
        {
            string IFoo<string>.IMethod1<T>(T t1, T t2) { return "MyStruct2.IFoo<string>.IMethod1<" + typeof(T) + ">(" + t1 + "," + t2 + ")"; }
            public string IMethod1<T>(T t1, T t2) { return "MyStruct2.IMethod1<" + typeof(T) + ">(" + t1 + "," + t2 + ")"; }
        }
        struct MyStruct3 : IFoo<string>, IFoo<int>
        {
            string IFoo<int>.IMethod1<T>(T t1, T t2) { return "MyStruct3.IFoo<int>.IMethod1<" + typeof(T) + ">(" + t1 + "," + t2 + ")"; }
            public string IMethod1<T>(T t1, T t2) { return "MyStruct3.IMethod1<" + typeof(T) + ">(" + t1 + "," + t2 + ")"; }
        }

        static string s_GMethod1;
        static string s_IFooString;
        static string s_IFooObject;
        static string s_IFooInt;

        static int s_NumErrors = 0;

        private static void TestWithStruct(IFoo<string> ifooStr, IFoo<object> ifooObj, IFoo<int> ifooInt)
        {
            var res = ifooStr.IMethod1<int>(1, 2);
            WriteLineWithVerification(res, s_IFooString);

            res = ifooObj.IMethod1<int>(3, 4);
            WriteLineWithVerification(res, s_IFooObject);

            res = ifooInt.IMethod1<int>(5, 6);
            WriteLineWithVerification(res, s_IFooInt);
        }

        private static void TestWithClass(object o)
        {
            Base b = o as Base;
            var res = b.GMethod1<int>(1, 2);
            WriteLineWithVerification(res, s_GMethod1);

            IFoo<string> ifoo1 = o as IFoo<string>;
            res = ifoo1.IMethod1<int>(3, 4);
            WriteLineWithVerification(res, s_IFooString);

            IFoo<object> ifoo2 = o as IFoo<object>;
            res = ifoo2.IMethod1<int>(5, 6);
            WriteLineWithVerification(res, s_IFooObject);

            IFoo<int> ifoo3 = o as IFoo<int>;
            res = ifoo3.IMethod1<int>(7, 8);
            WriteLineWithVerification(res, s_IFooInt);
        }

        private static void TestWithGenClass<T>(object o)
        {
            GenBase<T> b = o as GenBase<T>;
            var res = b.GMethod1<int>(1, 2);
            WriteLineWithVerification(res, s_GMethod1);

            IFoo<string> ifoo1 = o as IFoo<string>;
            res = ifoo1.IMethod1<int>(3, 4);
            WriteLineWithVerification(res, s_IFooString);

            IFoo<object> ifoo2 = o as IFoo<object>;
            res = ifoo2.IMethod1<int>(5, 6);
            WriteLineWithVerification(res, s_IFooObject);

            IFoo<int> ifoo3 = o as IFoo<int>;
            res = ifoo3.IMethod1<int>(7, 8);
            WriteLineWithVerification(res, s_IFooInt);
        }

        private static void WriteLineWithVerification(string actual, string expected)
        {
            if (actual != expected)
            {
                Console.WriteLine("ACTUAL   : " + actual);
                Console.WriteLine("EXPECTED : " + expected);
                s_NumErrors++;
            }
            else
            {
                Console.WriteLine(actual);
            }
        }

        public static void Run()
        {
            {
                s_GMethod1 = "Base.GMethod1<System.Int32>(1,2)";
                s_IFooString = "Base.IMethod1<System.Int32>(3,4)";
                s_IFooObject = "Base.IMethod1<System.Int32>(5,6)";
                s_IFooInt = "Base.IMethod1<System.Int32>(7,8)";
                TestWithClass(new Base());
                Console.WriteLine("====================");


                s_GMethod1 = "Derived.GMethod1<System.Int32>(1,2)";
                s_IFooString = "Derived.IFoo<string>.IMethod1<System.Int32>(3,4)";
                s_IFooObject = "Derived.IFoo<string>.IMethod1<System.Int32>(5,6)";
                s_IFooInt = "Base.IMethod1<System.Int32>(7,8)";
                TestWithClass(new Derived());
                Console.WriteLine("====================");


                s_GMethod1 = "Derived.GMethod1<System.Int32>(1,2)";
                s_IFooString = "Derived.IFoo<string>.IMethod1<System.Int32>(3,4)";
                s_IFooObject = "Derived.IFoo<string>.IMethod1<System.Int32>(5,6)";
                s_IFooInt = "SuperDerived.IFoo<int>.IMethod1<System.Int32>(7,8)";
                TestWithClass(new SuperDerived());
                Console.WriteLine("====================");
            }

            {
                s_GMethod1 = "GenBase<System.Byte>.GMethod1<System.Int32>(1,2)";
                s_IFooString = "GenBase<System.Byte>.IMethod1<System.Int32>(3,4)";
                s_IFooObject = "GenBase<System.Byte>.IMethod1<System.Int32>(5,6)";
                s_IFooInt = "GenBase<System.Byte>.IMethod1<System.Int32>(7,8)";
                TestWithGenClass<byte>(new GenBase<byte>());
                Console.WriteLine("====================");


                s_GMethod1 = "GenDerived<System.Byte>.GMethod1<System.Int32>(1,2)";
                s_IFooString = "GenDerived<System.Byte>.IFoo<string>.IMethod1<System.Int32>(3,4)";
                s_IFooObject = "GenDerived<System.Byte>.IFoo<string>.IMethod1<System.Int32>(5,6)";
                s_IFooInt = "GenBase<System.Byte>.IMethod1<System.Int32>(7,8)";
                TestWithGenClass<byte>(new GenDerived<byte>());
                Console.WriteLine("====================");


                s_GMethod1 = "GenDerived<System.String>.GMethod1<System.Int32>(1,2)";
                s_IFooString = "GenDerived<System.String>.IFoo<string>.IMethod1<System.Int32>(3,4)";
                s_IFooObject = "GenDerived<System.String>.IFoo<string>.IMethod1<System.Int32>(5,6)";
                s_IFooInt = "GenBase<System.String>.IMethod1<System.Int32>(7,8)";
                TestWithGenClass<String>(new GenDerived<String>());
                Console.WriteLine("====================");


                s_GMethod1 = "GenDerived<System.Byte>.GMethod1<System.Int32>(1,2)";
                s_IFooString = "GenDerived<System.Byte>.IFoo<string>.IMethod1<System.Int32>(3,4)";
                s_IFooObject = "GenDerived<System.Byte>.IFoo<string>.IMethod1<System.Int32>(5,6)";
                s_IFooInt = "GenSuperDerived<System.Byte>.IFoo<int>.IMethod1<System.Int32>(7,8)";
                TestWithGenClass<byte>(new GenSuperDerived<byte>());
                Console.WriteLine("====================");
            }

            {
                s_IFooString = "MyStruct1.IFoo<string>.IMethod1<System.Int32>(1,2)";
                s_IFooObject = "MyStruct1.IFoo<string>.IMethod1<System.Int32>(3,4)";
                s_IFooInt = "MyStruct1.IFoo<int>.IMethod1<System.Int32>(5,6)";
                TestWithStruct(new MyStruct1(), new MyStruct1(), new MyStruct1());
                Console.WriteLine("====================");


                s_IFooString = "MyStruct2.IFoo<string>.IMethod1<System.Int32>(1,2)";
                s_IFooObject = "MyStruct2.IFoo<string>.IMethod1<System.Int32>(3,4)";
                s_IFooInt = "MyStruct2.IMethod1<System.Int32>(5,6)";
                TestWithStruct(new MyStruct2(), new MyStruct2(), new MyStruct2());
                Console.WriteLine("====================");


                s_IFooString = "MyStruct3.IMethod1<System.Int32>(1,2)";
                s_IFooObject = "MyStruct3.IMethod1<System.Int32>(3,4)";
                s_IFooInt = "MyStruct3.IFoo<int>.IMethod1<System.Int32>(5,6)";
                TestWithStruct(new MyStruct3(), new MyStruct3(), new MyStruct3());
                Console.WriteLine("====================");
            }

            if (s_NumErrors != 0)
                throw new Exception();
        }
    }

    class TestGvmDependencies
    {
        class Atom { }

        class Foo
        {
            public virtual object Frob<T>()
            {
                return new T[0, 0];
            }
        }

        class Bar : Foo
        {
            public override object Frob<T>()
            {
                return new T[0, 0, 0];
            }
        }

        public static void Run()
        {
            {
                Foo x = new Foo();
                x.Frob<Atom>();
            }

            {
                Foo x = new Bar();
                x.Frob<Atom>();
            }
        }
    }

    class TestFieldAccess
    {
        class ClassType { }
        class ClassType2 { }
        struct StructType { }

        class Foo<T>
        {
            static Foo()
            {
                Console.WriteLine("Foo<" + typeof(T).Name + "> cctor");

                if (typeof(T) == typeof(ClassType))
                    TestFieldAccess.s_FooClassTypeCctorCount++;
                else
                    TestFieldAccess.s_FooStructTypeCctorCount++;
            }

            public static int s_intField;
            public static float s_floatField;
            public static string s_stringField;
            public static object s_objectField;
            public static long s_longField1;
            public static long s_longField2;
            public static long s_longField3;
            public static KeyValuePair<string, string> s_kvp;

            public int m_intField;
            public float m_floatField;
            public string m_stringField;
            public object m_objectField;
        }

        // Remove this and use Foo once we have type loader support for all necessary dictionary slots
        class FooDynamic<T>
        {
            static FooDynamic()
            {
                Console.WriteLine("FooDynamic<" + typeof(T).Name + "> cctor");
            }

            public static int s_intField;
            public static float s_floatField;
            public static long s_longField1;
        }

        class Bar
        {
            static Bar()
            {
                Console.WriteLine("Bar cctor");
                TestFieldAccess.s_BarCctorCount++;
            }

            public static int s_intField;
            public static float s_floatField;
            public static string s_stringField;
            public static object s_objectField;
            public static long s_longField1;
            public static long s_longField2;
            public static long s_longField3;
            public static KeyValuePair<string, string> s_kvp;

            public int m_intField;
            public float m_floatField;
            public string m_stringField;
            public object m_objectField;
        }

        public static int s_FooClassTypeCctorCount = 0;
        public static int s_FooStructTypeCctorCount = 0;
        public static int s_BarCctorCount = 0;
        public static int s_NumErrors = 0;

        private static void Verify<T>(T expected, T actual)
        {
            if (!actual.Equals(expected))
            {
                Console.WriteLine("ACTUAL   : " + actual);
                Console.WriteLine("EXPECTED : " + expected);
                s_NumErrors++;
            }
        }

        private static void TestDynamicStaticFields()
        {
            FooDynamic<object>.s_intField = 1234;
            FooDynamic<object>.s_floatField = 12.34f;
            FooDynamic<object>.s_longField1 = 0x1111;

            var fooDynamicOfClassType = typeof(FooDynamic<>).MakeGenericType(typeof(ClassType)).GetTypeInfo();
            var fooDynamicOfClassType2 = typeof(FooDynamic<>).MakeGenericType(typeof(ClassType2)).GetTypeInfo();
            
            FieldInfo fi = fooDynamicOfClassType.GetDeclaredField("s_intField");
            FieldInfo fi2 = fooDynamicOfClassType2.GetDeclaredField("s_intField");
            fi.SetValue(null, 1111);
            fi2.SetValue(null, 2222);
            Verify(1111, (int)fi.GetValue(null));
            Verify(2222, (int)fi2.GetValue(null));

            fi = fooDynamicOfClassType.GetDeclaredField("s_floatField");
            fi2 = fooDynamicOfClassType2.GetDeclaredField("s_floatField");
            fi.SetValue(null, 1.1f);
            fi2.SetValue(null, 2.2f);
            Verify(1.1f, (float)fi.GetValue(null));
            Verify(2.2f, (float)fi2.GetValue(null));

            fi = fooDynamicOfClassType.GetDeclaredField("s_longField1");
            fi2 = fooDynamicOfClassType2.GetDeclaredField("s_longField1");
            fi.SetValue(null, 0x11111111);
            fi2.SetValue(null, 0x22222222);
            Verify(0x11111111, (long)fi.GetValue(null));
            Verify(0x22222222, (long)fi2.GetValue(null));
        }

        private static void TestStaticFields()
        {
            Foo<ClassType>.s_intField = 11223344;
            Foo<ClassType>.s_stringField = "abcd";
            Foo<ClassType>.s_floatField = 12.34f;
            Foo<ClassType>.s_objectField = "123";
            Foo<ClassType>.s_kvp = new KeyValuePair<string, string>("1122", "3344");

            Foo<StructType>.s_intField = 44332211;
            Foo<StructType>.s_stringField = "dcba";
            Foo<StructType>.s_floatField = 43.21f;
            Foo<StructType>.s_objectField = "321";
            Foo<StructType>.s_kvp = new KeyValuePair<string, string>("4433", "2211");


            Bar.s_intField = 778899;
            Bar.s_stringField = "xxyyzz";
            Bar.s_floatField = 88.99f;
            Bar.s_objectField = "890";
            Bar.s_kvp = new KeyValuePair<string, string>("7788", "8899");

            // Testing correctness of cctor context
            {
                Foo<ClassType>.s_longField1 = 0xff00;
                Foo<ClassType>.s_longField2 = 0xff00;
                Foo<ClassType>.s_longField3 = 0xff00;
                if (TestFieldAccess.s_FooClassTypeCctorCount != 1)
                    s_NumErrors++;

                Foo<StructType>.s_longField1 = 0xff00;
                Foo<StructType>.s_longField2 = 0xff00;
                Foo<StructType>.s_longField3 = 0xff00;
                if (TestFieldAccess.s_FooStructTypeCctorCount != 1)
                    s_NumErrors++;

                Bar.s_longField1 = 0xff00;
                Bar.s_longField2 = 0xff00;
                Bar.s_longField3 = 0xff00;
                if (TestFieldAccess.s_BarCctorCount != 1)
                    s_NumErrors++;
            }

            Console.WriteLine("Testing static fields on type Foo<ClassType> ...");
            {
                FieldInfo fi = typeof(Foo<ClassType>).GetTypeInfo().GetDeclaredField("s_intField");
                Verify((int)11223344, (int)fi.GetValue(null));

                fi = typeof(Foo<ClassType>).GetTypeInfo().GetDeclaredField("s_stringField");
                Verify("abcd", (string)fi.GetValue(null));

                fi = typeof(Foo<ClassType>).GetTypeInfo().GetDeclaredField("s_floatField");
                Verify(12.34f, (float)fi.GetValue(null));

                fi = typeof(Foo<ClassType>).GetTypeInfo().GetDeclaredField("s_objectField");
                Verify("123", fi.GetValue(null));

                fi = typeof(Foo<ClassType>).GetTypeInfo().GetDeclaredField("s_kvp");
                var result = (KeyValuePair<string, string>)fi.GetValue(null);
                Verify("1122", result.Key);
                Verify("3344", result.Value);

                typeof(Foo<ClassType>).GetTypeInfo().GetDeclaredField("s_stringField").SetValue(null, "ThisIsAString1");
                typeof(Foo<ClassType>).GetTypeInfo().GetDeclaredField("s_objectField").SetValue(null, "ThisIsAString2");
                typeof(Foo<ClassType>).GetTypeInfo().GetDeclaredField("s_kvp").SetValue(null, new KeyValuePair<string, string>("ThisIs", "AString"));
                Verify("ThisIsAString1", (string)Foo<ClassType>.s_stringField);
                Verify("ThisIsAString2", (string)Foo<ClassType>.s_objectField);
                Verify("ThisIs", (string)Foo<ClassType>.s_kvp.Key);
                Verify("AString", (string)Foo<ClassType>.s_kvp.Value);
            }

            Console.WriteLine("Testing static fields on type Foo<StructType> ...");
            {
                FieldInfo fi = typeof(Foo<StructType>).GetTypeInfo().GetDeclaredField("s_intField");
                Verify(44332211, (int)fi.GetValue(null));

                fi = typeof(Foo<StructType>).GetTypeInfo().GetDeclaredField("s_stringField");
                Verify("dcba", (string)fi.GetValue(null));

                fi = typeof(Foo<StructType>).GetTypeInfo().GetDeclaredField("s_floatField");
                Verify(43.21f, (float)fi.GetValue(null));

                fi = typeof(Foo<StructType>).GetTypeInfo().GetDeclaredField("s_objectField");
                Verify("321", fi.GetValue(null));

                fi = typeof(Foo<StructType>).GetTypeInfo().GetDeclaredField("s_kvp");
                var result = (KeyValuePair<string, string>)fi.GetValue(null);
                Verify("4433", result.Key);
                Verify("2211", result.Value);

                typeof(Foo<StructType>).GetTypeInfo().GetDeclaredField("s_stringField").SetValue(null, "ThisIsAString3");
                typeof(Foo<StructType>).GetTypeInfo().GetDeclaredField("s_objectField").SetValue(null, "ThisIsAString4");
                typeof(Foo<StructType>).GetTypeInfo().GetDeclaredField("s_kvp").SetValue(null, new KeyValuePair<string, string>("ThisIs1", "AString1"));
                Verify("ThisIsAString3", (string)Foo<StructType>.s_stringField);
                Verify("ThisIsAString4", (string)Foo<StructType>.s_objectField);
                Verify("ThisIs1", (string)Foo<StructType>.s_kvp.Key);
                Verify("AString1", (string)Foo<StructType>.s_kvp.Value);
            }

            Console.WriteLine("Testing static fields on type Bar ...");
            {
                FieldInfo fi = typeof(Bar).GetTypeInfo().GetDeclaredField("s_intField");
                Verify(778899, (int)fi.GetValue(null));

                fi = typeof(Bar).GetTypeInfo().GetDeclaredField("s_stringField");
                Verify("xxyyzz", (string)fi.GetValue(null));

                fi = typeof(Bar).GetTypeInfo().GetDeclaredField("s_floatField");
                Verify(88.99f, (float)fi.GetValue(null));

                fi = typeof(Bar).GetTypeInfo().GetDeclaredField("s_objectField");
                Verify("890", fi.GetValue(null));

                fi = typeof(Bar).GetTypeInfo().GetDeclaredField("s_kvp");
                var result = (KeyValuePair<string, string>)fi.GetValue(null);
                Verify("7788", result.Key);
                Verify("8899", result.Value);

                typeof(Bar).GetTypeInfo().GetDeclaredField("s_stringField").SetValue(null, "ThisIsAString5");
                typeof(Bar).GetTypeInfo().GetDeclaredField("s_objectField").SetValue(null, "ThisIsAString6");
                typeof(Bar).GetTypeInfo().GetDeclaredField("s_kvp").SetValue(null, new KeyValuePair<string, string>("ThisIs2", "AString2"));
                Verify("ThisIsAString5", (string)Bar.s_stringField);
                Verify("ThisIsAString6", (string)Bar.s_objectField);
                Verify("ThisIs2", (string)Bar.s_kvp.Key);
                Verify("AString2", (string)Bar.s_kvp.Value);
            }
        }

        private static void TestInstanceFields()
        {
            Foo<ClassType> fooClassType = new Foo<ClassType>
            {
                m_intField = 1212,
                m_stringField = "2323",
                m_floatField = 34.34f,
                m_objectField = "4545",
            };

            Foo<StructType> fooStructType = new Foo<StructType>
            {
                m_intField = 2323,
                m_stringField = "3434",
                m_floatField = 45.45f,
                m_objectField = "5656",
            };

            Bar bar = new Bar
            {
                m_intField = 3434,
                m_stringField = "4545",
                m_floatField = 56.56f,
                m_objectField = "6767",
            };

            Console.WriteLine("Testing instance fields on type Foo<ClassType> ...");
            {
                FieldInfo fi = typeof(Foo<ClassType>).GetTypeInfo().GetDeclaredField("m_intField");
                Verify(1212, (int)fi.GetValue(fooClassType));

                fi = typeof(Foo<ClassType>).GetTypeInfo().GetDeclaredField("m_stringField");
                Verify("2323", (string)fi.GetValue(fooClassType));

                fi = typeof(Foo<ClassType>).GetTypeInfo().GetDeclaredField("m_floatField");
                Verify(34.34f, (float)fi.GetValue(fooClassType));

                fi = typeof(Foo<ClassType>).GetTypeInfo().GetDeclaredField("m_objectField");
                Verify("4545", fi.GetValue(fooClassType));

                typeof(Foo<ClassType>).GetTypeInfo().GetDeclaredField("m_stringField").SetValue(fooClassType, "ThisIsAString7");
                typeof(Foo<ClassType>).GetTypeInfo().GetDeclaredField("m_objectField").SetValue(fooClassType, "ThisIsAString8");
                Verify("ThisIsAString7", (string)fooClassType.m_stringField);
                Verify("ThisIsAString8", (string)fooClassType.m_objectField);
            }

            Console.WriteLine("Testing instance fields on type Foo<StructType> ...");
            {
                FieldInfo fi = typeof(Foo<StructType>).GetTypeInfo().GetDeclaredField("m_intField");
                Verify(2323, (int)fi.GetValue(fooStructType));

                fi = typeof(Foo<StructType>).GetTypeInfo().GetDeclaredField("m_stringField");
                Verify("3434", (string)fi.GetValue(fooStructType));

                fi = typeof(Foo<StructType>).GetTypeInfo().GetDeclaredField("m_floatField");
                Verify(45.45f, (float)fi.GetValue(fooStructType));

                fi = typeof(Foo<StructType>).GetTypeInfo().GetDeclaredField("m_objectField");
                Verify("5656", fi.GetValue(fooStructType));

                typeof(Foo<StructType>).GetTypeInfo().GetDeclaredField("m_stringField").SetValue(fooStructType, "ThisIsAString9");
                typeof(Foo<StructType>).GetTypeInfo().GetDeclaredField("m_objectField").SetValue(fooStructType, "ThisIsAString10");
                Verify("ThisIsAString9", (string)fooStructType.m_stringField);
                Verify("ThisIsAString10", (string)fooStructType.m_objectField);
            }

            Console.WriteLine("Testing instance fields on type Bar ...");
            {
                FieldInfo fi = typeof(Bar).GetTypeInfo().GetDeclaredField("m_intField");
                Verify(3434, (int)fi.GetValue(bar));

                fi = typeof(Bar).GetTypeInfo().GetDeclaredField("m_stringField");
                Verify("4545", (string)fi.GetValue(bar));

                fi = typeof(Bar).GetTypeInfo().GetDeclaredField("m_floatField");
                Verify(56.56f, (float)fi.GetValue(bar));

                fi = typeof(Bar).GetTypeInfo().GetDeclaredField("m_objectField");
                Verify("6767", fi.GetValue(bar));

                typeof(Bar).GetTypeInfo().GetDeclaredField("m_stringField").SetValue(bar, "ThisIsAString11");
                typeof(Bar).GetTypeInfo().GetDeclaredField("m_objectField").SetValue(bar, "ThisIsAString12");
                Verify("ThisIsAString11", (string)bar.m_stringField);
                Verify("ThisIsAString12", (string)bar.m_objectField);
            }
        }

        public static void Run()
        {
            TestStaticFields();
            TestInstanceFields();
            TestDynamicStaticFields();

            if (s_NumErrors != 0)
                throw new Exception(s_NumErrors + " errors!");
        }
    }
}
