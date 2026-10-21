using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
namespace MiniPdf
{
    internal static partial class SelfTest
    {
        static MergePlan ManyPlan(string directory)
        {
            var plan = ScanPlan(directory, true);
            plan.Add(PdfDocument.Open(Path.Combine(directory, "scan-third.pdf"), ""), "C-extra.pdf");
            plan.Groups.Insert(1, new MergeGroup { Name = "Empty" });
            return plan;
        }
        static void ManyMerge(string directory)
        {
            using (var plan = ManyPlan(directory))
            {
                int count = plan.Count, history = MergeHistory(plan);
                plan.Groups[0].Pages[1].Rotation = 1;
                var original = plan.AllPages().ToArray();
                plan.Arrange(5);
                Assert(MergeSequence(plan) == "A1,B1,C1,A2,B2,A3,B3,B4",
                       "all-file page interleave includes three unequal inputs in each round");
                Assert(plan.Count == count && plan.AllPages().Distinct().Count() == count &&
                           original.All(p => plan.AllPages().Contains(p)),
                       "all-file interleave retains each original page exactly once");
                Assert(plan.Groups.Count == 2 && plan.Groups[1].Name == "Empty" && plan.Groups[0].Expanded,
                       "multi-file interleave replaces all non-empty groups and preserves empty groups");
                Assert(plan.AllPages().Single(p => p.Name == "A-odd.pdf" && p.Index == 1).Rotation == 1,
                       "multi-file interleave preserves page rotations");
                Assert(MergeHistory(plan) == history + 1, "multi-file arrangement is one undo step");
                SaveMerge(plan, directory, "interleave-all.pdf");
                plan.Undo();
                Assert(plan.Groups.Count == 4 && MergeSequence(plan) == "A1,A2,A3,B1,B2,B3,B4,C1",
                       "undo restores all original file containers and order");
                plan.Redo();
                Assert(MergeSequence(plan) == "A1,B1,C1,A2,B2,A3,B3,B4", "redo restores all-file interleave");
            }
            using (var plan = ManyPlan(directory))
            {
                plan.Arrange(6, 2);
                Assert(MergeSequence(plan) == "A1,A2,B1,B2,C1,A3,B3,B4",
                       "all-file block interleave handles incomplete blocks without losing pages");
                SaveMerge(plan, directory, "interleave-all-blocks.pdf");
                plan.Arrange(0);
                Assert(plan.Groups.Count == 3 && MergeSequence(plan) == "A1,A2,A3,B1,B2,B3,B4,C1",
                       "regrouping after multi-file interleave reconstructs each original source");
            }
            using (var plan = ManyPlan(directory))
            {
                plan.Arrange(6, 10000);
                Assert(MergeSequence(plan) == "A1,A2,A3,B1,B2,B3,B4,C1",
                       "a block larger than every input appends all files sequentially");
                int before = MergeHistory(plan);
                bool invalid = false;
                try
                {
                    plan.Arrange(6, 0);
                }
                catch (InvalidOperationException)
                {
                    invalid = true;
                }
                Assert(invalid && before == MergeHistory(plan), "invalid all-file block size leaves history unchanged");
            }
            using (var plan = ManyPlan(directory))
            {
                plan.Groups.RemoveAt(1);
                plan.Groups[0].Name = "scan10.pdf";
                plan.Groups[1].Name = "scan2.pdf";
                plan.Groups[2].Name = "scan1.pdf";
                plan.Arrange(7);
                Assert(MergeSequence(plan) == "C1,B1,B2,B3,B4,A1,A2,A3",
                       "natural filename sort orders 1, 2, 10 while preserving pages inside files");
                int before = MergeHistory(plan);
                plan.Arrange(7);
                Assert(before == MergeHistory(plan), "sorting an already sorted merge does not consume undo history");
                SaveMerge(plan, directory, "merge-natural-order.pdf");
                plan.Undo();
                Assert(MergeSequence(plan) == "A1,A2,A3,B1,B2,B3,B4,C1",
                       "undo restores file order before natural sorting");
            }
            using (var plan = ManyPlan(directory))
            {
                plan.Arrange(8);
                Assert(MergeSequence(plan) == "A3,A2,A1,B4,B3,B2,B1,C1",
                       "reverse-within-files retains file order and single-page inputs");
                SaveMerge(plan, directory, "merge-reversed-files.pdf");
                plan.Undo();
                Assert(MergeSequence(plan) == "A1,A2,A3,B1,B2,B3,B4,C1", "reverse-within-files supports one-step undo");
            }
            using (var plan = ManyPlan(directory))
            {
                for (int i = 0; i < 2; i++)
                    plan.Add(PdfDocument.Open(Path.Combine(directory, "scan-third.pdf"), ""),
                             i == 0 ? "D-extra.pdf" : "E-extra.pdf");
                plan.Arrange(5);
                Assert(MergeSequence(plan) == "A1,B1,C1,D1,E1,A2,B2,A3,B3,B4",
                       "all-file interleave includes every one of five documents");
            }
            using (var source = PdfDocument.Open(Path.Combine(directory, "baseline.pdf"), ""))
            {
                var pages = new List<MergePage>();
                for (int i = 0; i < 12; i++)
                    pages.Add(new MergePage { Source = source, Name = "duplicate.pdf", Index = 0, Rotation = i % 4 });
                using (var output = PdfDocument.MergePages(pages))
                {
                    Assert(output.Count == 12, "batched duplicate imports retain every requested page");
                    output.Save(Path.Combine(directory, "batched-duplicates.pdf"));
                    output.OpenPage(0);
                    output.AddText("FIRST_COPY_ONLY", 60, 300, 16, "自动", System.Drawing.Color.Red);
                    output.OpenPage(4);
                    Assert(!output.SelectedText(0, 10000).Contains("FIRST_COPY_ONLY"),
                           "editing one imported duplicate cannot change another duplicate");
                    output.OpenPage(0);
                    Assert(output.SelectedText(0, 10000).Contains("FIRST_COPY_ONLY"),
                           "first duplicate retains its own independent edit");
                    output.Save(Path.Combine(directory, "batched-duplicates-edited.pdf"));
                }
            }
            using (var source = PdfDocument.Open(Path.Combine(directory, "shared-resources.pdf"), ""))
            {
                var pages = new List<MergePage>();
                for (int i = 0; i < source.Count; i++)
                    pages.Add(new MergePage { Source = source, Name = "shared.pdf", Index = i });
                using (var output = PdfDocument.MergePages(pages))
                {
                    Assert(output.Serialize(false).Length < source.Serialize(false).Length * 2,
                           "batch import retains shared image and font resources across distinct pages");
                    output.Save(Path.Combine(directory, "batched-resources.pdf"));
                }
            }
            using (var source = PdfDocument.Open(Path.Combine(directory, "indirect-note.pdf"), ""))
            {
                var pages = new List<MergePage> { new MergePage { Source = source, Name = "note.pdf", Index = 0 },
                                                  new MergePage { Source = source, Name = "note.pdf", Index = 0 } };
                using (var output = PdfDocument.MergePages(pages))
                {
                    output.EditNote(new Pick { AnnotIndex = 0, Type = 1 }, "FIRST_NOTE_ONLY");
                    Assert(output.PickObject(new N.Pt(50, 360)).Text == "FIRST_NOTE_ONLY",
                           "first imported indirect note retains its edit");
                    output.OpenPage(1);
                    Assert(output.PickObject(new N.Pt(50, 360)).Text == "ORIGINAL_NOTE",
                           "editing an indirect note never changes another imported copy");
                    output.Save(Path.Combine(directory, "batched-notes.pdf"));
                }
                Assert(source.PickObject(new N.Pt(50, 360)).Text == "ORIGINAL_NOTE",
                       "editing an imported note leaves its source unchanged");
            }
            UiText.English = true;
            using (var dialog = new MergeWindow(null))
            {
                dialog.Plan.Add(PdfDocument.Open(Path.Combine(directory, "scan-odd-short.pdf"), ""), "A.pdf");
                dialog.Plan.Add(PdfDocument.Open(Path.Combine(directory, "scan-even.pdf"), ""), "B.pdf");
                dialog.Plan.Add(PdfDocument.Open(Path.Combine(directory, "scan-third.pdf"), ""), "C.pdf");
                dialog.Show();
                Application.DoEvents();
                var menu = (ContextMenuStrip)MergeValue(dialog, "arrangeMenu");
                for (int i = 5; i < 9; i++)
                {
                    var item = (ToolStripMenuItem)menu.Items[i + 1];
                    Assert(item.ShortcutKeyDisplayString == "Ctrl+" + (i + 1) &&
                               !System.Text.RegularExpressions.Regex.IsMatch(item.Text, "[\\u4e00-\\u9fff]"),
                           "multi-file mode " + i + " displays its shortcut and English label");
                }
                MergeKey(dialog.Controls.OfType<MergeWindow.MergeBoard>().Single(), Keys.Control | Keys.D6);
                Assert(MergeSequence(dialog.Plan) == "A1,B1,C1,A2,B2,A3,B3,B4",
                       "actual Ctrl+6 invokes all-file interleave inside the workspace");
                dialog.Plan.Undo();
                MergeKey(dialog.Controls.OfType<MergeWindow.MergeBoard>().Single(), Keys.Control | Keys.D9);
                Assert(MergeSequence(dialog.Plan) == "A3,A2,A1,B4,B3,B2,B1,C1",
                       "actual Ctrl+9 reverses each file without changing file order");
                dialog.DialogResult = DialogResult.OK;
                dialog.Close();
            }
        }
    }
}
