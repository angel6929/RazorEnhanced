using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;

// Loads the real x86 release assembly. No UI, hooks, network, or installed settings.
public static class JournalLifetimeRegression
{
    private const BindingFlags Static = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static ConstructorInfo journalConstructor, entryConstructor, weakConstructor;
    private static MethodInfo enqueue, clear, tryGetTarget;
    private static PropertyInfo active, text;
    private static FieldInfo queue;
    private static object global;
    private static IList instances;
    private static int passed, failed;

    private static void Check(string name, Action action)
    {
        try { action(); passed++; Console.WriteLine("PASS " + name); }
        catch (Exception ex) { failed++; Console.WriteLine("FAIL " + name + ": " + ex.Message); }
    }

    private static void Expect(bool value, string message)
    {
        if (!value) throw new Exception(message);
    }

    private static object NewJournal(int size)
    {
        return journalConstructor.Invoke(new object[] { size });
    }

    private static object Entry(string value)
    {
        return entryConstructor.Invoke(new object[] { value, "Spell", 0, "Audit", 1 });
    }

    private static void Send(string value)
    {
        enqueue.Invoke(null, new object[] { Entry(value) });
    }

    private static string[] Texts(object journal)
    {
        List<string> result = new List<string>();
        foreach (object entry in (IEnumerable)queue.GetValue(journal))
            result.Add((string)text.GetValue(entry, null));
        return result.ToArray();
    }

    private static void Reset()
    {
        instances.Clear();
        instances.Add(weakConstructor.Invoke(new object[] { global }));
        clear.Invoke(global, null);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void AbandonJournals(int count)
    {
        for (int i = 0; i < count; i++) NewJournal(100);
    }

    private static int DeadCount()
    {
        int dead = 0;
        foreach (object weak in instances)
        {
            object[] target = new object[] { null };
            if (!(bool)tryGetTarget.Invoke(weak, target)) dead++;
        }
        return dead;
    }

    private static void Collect()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    public static int Run(string assemblyPath, bool includePressure)
    {
        Assembly assembly = Assembly.LoadFrom(assemblyPath);
        Type journal = assembly.GetType("RazorEnhanced.Journal", true);
        Type entry = journal.GetNestedType("JournalEntry");
        Type weak = typeof(WeakReference<>).MakeGenericType(journal);
        journalConstructor = journal.GetConstructor(new Type[] { typeof(int) });
        entryConstructor = entry.GetConstructor(new Type[] { typeof(string), typeof(string), typeof(int), typeof(string), typeof(int) });
        weakConstructor = weak.GetConstructor(new Type[] { journal });
        tryGetTarget = weak.GetMethod("TryGetTarget");
        enqueue = journal.GetMethod("Enqueue", Static);
        clear = journal.GetMethod("Clear", Type.EmptyTypes);
        active = journal.GetProperty("Active", Instance);
        queue = journal.GetField("m_journal", Instance);
        text = entry.GetProperty("Text");
        global = journal.GetField("GlobalJournal", Static).GetValue(null);
        instances = (IList)journal.GetField("allInstances", Static).GetValue(null);
        Console.WriteLine("ASSEMBLY " + assemblyPath);

        Check("live journals retain all ordered messages while dead journals are collected", delegate
        {
            Reset();
            object first = NewJournal(100), second = NewJournal(100);
            AbandonJournals(20);
            Collect();
            Expect(DeadCount() == 20, "The controlled journals were not collected.");
            for (int i = 0; i < 16; i++) Send("message-" + i);
            string[] a = Texts(first), b = Texts(second);
            Expect(a.Length == 16 && b.Length == 16, "A live subscriber lost messages.");
            for (int i = 0; i < 16; i++)
                Expect(a[i] == "message-" + i && b[i] == a[i], "Message order changed.");
            GC.KeepAlive(first); GC.KeepAlive(second);
        });

        Check("inactive journals are preserved but receive no new messages", delegate
        {
            Reset();
            object subscriber = NewJournal(100);
            Send("old");
            active.SetValue(subscriber, false, null);
            AbandonJournals(10); Collect(); Send("while-disabled");
            Expect(Texts(subscriber).Length == 0, "Inactive journal was populated.");
            bool found = false;
            foreach (object weakReference in instances)
            {
                object[] target = new object[] { null };
                if ((bool)tryGetTarget.Invoke(weakReference, target) && ReferenceEquals(target[0], subscriber)) found = true;
            }
            Expect(found, "Inactive live subscriber was removed.");
            active.SetValue(subscriber, true, null);
            Expect(String.Join("|", Texts(subscriber)) == "old|while-disabled", "Reactivation no longer clones global history.");
            Send("after-enable");
            Expect(String.Join("|", Texts(subscriber)) == "old|while-disabled|after-enable", "Reactivated subscriber missed message.");
            GC.KeepAlive(subscriber);
        });

        Check("journal entry capacity and clear remain unchanged", delegate
        {
            Reset(); object subscriber = NewJournal(5);
            AbandonJournals(10); Collect();
            for (int i = 0; i < 12; i++) Send("message-" + i);
            Expect(String.Join("|", Texts(subscriber)) == "message-7|message-8|message-9|message-10|message-11", "Capacity changed.");
            clear.Invoke(subscriber, null); Send("after-clear");
            Expect(String.Join("|", Texts(subscriber)) == "after-clear", "Clear changed.");
            GC.KeepAlive(subscriber);
        });

        int[] scales = includePressure ? new int[] { 10, 100, 1000, 10000, 100000 } : new int[] { 10, 100, 1000, 10000 };
        foreach (int count in scales)
        {
            Reset(); Send("warmup");
            object[] entryArgs = new object[] { Entry("benchmark") };
            for (int i = 0; i < 100; i++) enqueue.Invoke(null, entryArgs);
            AbandonJournals(count); Collect();
            int before = DeadCount();
            Stopwatch timer = Stopwatch.StartNew();
            enqueue.Invoke(null, entryArgs);
            timer.Stop(); double firstMs = timer.Elapsed.TotalMilliseconds;
            timer.Restart();
            for (int i = 0; i < 1000; i++) enqueue.Invoke(null, entryArgs);
            timer.Stop();
            Console.WriteLine("TIMING dead_before={0} first_ms={1:F4} next1000_ms={2:F4} instances_after={3}", before, firstMs, timer.Elapsed.TotalMilliseconds, instances.Count);
            int expected = count;
            Check("dead reference cleanup at " + count, delegate
            {
                Expect(before == expected, "Controlled GC count mismatch.");
                Expect(instances.Count == 1, "Dead references survived production Enqueue: " + (instances.Count - 1));
            });
        }
        Console.WriteLine("JOURNAL_LIFETIME_REGRESSION passed={0} failed={1}", passed, failed);
        return failed == 0 ? 0 : 1;
    }
}
