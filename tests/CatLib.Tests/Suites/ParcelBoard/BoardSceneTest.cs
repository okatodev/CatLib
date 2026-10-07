using System.Collections.Generic;
using System.Linq;
using CatLib.Game;
using CatLib.Tests.Framework;
using CatLib.UI;
using ParcelBoard;
using ParcelBoard.Logic;

namespace CatLib.Tests.Suites.ParcelBoard;

public sealed class BoardSceneTest : TestCase
{
    public override string Suite => "ParcelBoard";

    public override int Order => 50;

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        var controller = ParcelBoardPlugin.Controller;
        if (controller == null || !Singleton<InterfaceManager>.HasInstance() || !CatParcels.IsAvailable)
        {
            context.Note("The parcel board can only be checked in a level with Parcel Board installed");
            yield break;
        }

        yield return Wait.Until(() => HudLayer.IsAvailable, 5, "the HUD layer");
        var parcels = CatParcels.Read();
        var total = Singleton<ParcelManager>.Instance.GetAllParcels().Count;
        context.Note($"Parcels: {parcels.Count} read of {total} in the game; " +
                     string.Join(", ", parcels.GroupBy(parcel => parcel.Place).Select(group => group.Key + " " + group.Count())));
        Assert.Equal(total, parcels.Count, "Every parcel of the game is read");
        Assert.True(parcels.All(parcel => parcel.Region != ParcelRegion.None), "Every parcel has a destination");
        Assert.True(parcels.All(parcel => parcel.Footprint.IsKnown), "Every parcel has a footprint on the shelf");
        foreach (var group in parcels.GroupBy(parcel => parcel.Size).OrderBy(group => BoardCounter.SizeOrder.ToList().IndexOf(group.Key)))
        {
            Assert.NotNull(global::ParcelBoard.Scene.BoardView.SizeIcon(group.Key), $"The size icon of {group.Key} loads");
            context.Note($"{group.Key}: {group.Count()} parcel(s), {string.Join(" / ", group.Select(parcel => parcel.Footprint.Text('x')).Distinct())}");
        }

        Assert.NotNull(global::ParcelBoard.Scene.BoardView.SizesIcon(), "The header icon of the sizes loads");
        foreach (var region in parcels.Select(parcel => parcel.Region).Distinct())
        {
            Assert.NotNull(CatParcels.RegionIcon(region), $"The game has a stamp icon for {region}");
            context.Note($"{region}: {CatParcels.RegionName(region)}");
        }

        foreach (var constraint in CatParcels.StorageConstraints)
        {
            context.Note($"{constraint} icon: {(CatParcels.ConstraintIcon(constraint) == null ? "none" : CatParcels.ConstraintIcon(constraint).name)}");
        }

        foreach (var constraint in CatParcels.BehaviorConstraints)
        {
            context.Note($"{constraint} icon: {(CatParcels.ConstraintIcon(constraint) == null ? "none" : CatParcels.ConstraintIcon(constraint).name)}");
        }

        foreach (var list in System.Enum.GetValues(typeof(BoardList)).Cast<BoardList>().Where(list => list != BoardList.Sizes))
        {
            Assert.NotNull(global::ParcelBoard.Scene.BoardView.ListIcon(list), $"The list {list} has an icon, from the game or the mod");
        }

        var wasHidden = controller.IsHidden;
        var wasOpen = controller.IsOpen;
        var wasEnabled = controller.Settings.Enabled.Value;
        try
        {
            controller.Settings.Enabled.LocalValue = true;
            if (controller.IsHidden)
            {
                controller.ToggleHidden();
            }

            if (controller.IsOpen)
            {
                controller.ToggleOpen();
            }

            controller.Refresh();
            yield return Wait.Frames(2);
            Assert.True(controller.View.IsAlive, "The board is on the HUD");
            var all = controller.LastColumns.FirstOrDefault(column => column.List == BoardList.All);
            Assert.NotNull(all, "The list of all parcels is shown");
            Assert.Equal(BoardCounter.Build(CatParcels.Read(), controller.Settings.Options()).First(column => column.List == BoardList.All).Total, all.Total, "The total matches the parcels");
            Assert.NotNull(controller.View.Main, "The table of destinations is shown");
            var closed = controller.View.Main.Size;

            controller.ToggleOpen();
            controller.Refresh();
            yield return Wait.Frames(2);
            var open = controller.View.Main.Size;
            if (controller.View.Sizes != null)
            {
                var sizes = controller.View.Sizes.Size;
                context.Note($"Sizes table {sizes.x:0}x{sizes.y:0} next to the destinations");
                Assert.True(UnityEngine.Mathf.Abs(sizes.y - open.y) < 0.5f, "The table of sizes is as tall as the table of destinations");
            }

            context.Note($"All: {all.Total}, closed {closed.x:0}x{closed.y:0}, open {open.x:0}x{open.y:0}, {controller.LastColumns.Count} list(s): " +
                         string.Join(", ", controller.LastColumns.Select(column => column.List + " " + column.Total)));
            if (all.Rows.Count > 0)
            {
                Assert.True(open.y > closed.y, "An open table is taller than a closed one");
            }

            var screen = controller.View.Rect.parent.TryCast<UnityEngine.RectTransform>().rect;
            var board = controller.View.Rect.rect;
            context.Note($"Board {board.width:0}x{board.height:0} on a {screen.width:0}x{screen.height:0} HUD");
            Assert.True(board.width <= screen.width - 2f * global::ParcelBoard.Scene.BoardView.Margin, "The open board fits the width of the screen");

            controller.ToggleHidden();
            controller.Refresh();
            yield return Wait.Frames(2);
            Assert.False(controller.View.Rect.gameObject.activeSelf, "The key hides the board");
        }
        finally
        {
            controller.Settings.Enabled.LocalValue = wasEnabled;
            if (controller.IsHidden != wasHidden)
            {
                controller.ToggleHidden();
            }

            if (controller.IsOpen != wasOpen)
            {
                controller.ToggleOpen();
            }
        }
    }
}
