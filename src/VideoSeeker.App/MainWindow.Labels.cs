using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using Ellipse = System.Windows.Shapes.Ellipse;
using Rectangle = System.Windows.Shapes.Rectangle;
using VideoSeeker.App.Data;

namespace VideoSeeker.App;

/// <summary>
/// Etiquetas de bookmarks ("Boss", "Puzzle", "Cinemática"…): cada colección tiene las suyas, con nombre y color. Un
/// bookmark con etiqueta se pinta con su color. Se ponen en el cuadro del nombre (clic o Alt + número), con Alt + 1…9
/// sobre el seleccionado o desde su menú; en el panel, unos botoncitos filtran la lista y, con clic derecho, las gestionan.
/// </summary>
public partial class MainWindow
{
    private LabelFile _labels = new();
    // Etiquetas por las que filtra el panel ("" = sin etiqueta); vacío, sin filtro.
    private readonly HashSet<string> _labelFilter = [];
    // Etiqueta elegida en el cuadro del nombre (se guarda con el nombre al aceptarlo).
    private string? _namingLabel;

    private void InitLabels()
    {
        Seek.ColorOf = Zoom.ColorOf = ColorOf;
        LoadLabels();
    }

    /// <summary>Color con el que se pinta un bookmark: el de su etiqueta, o el suyo.</summary>
    private string ColorOf(Bookmark b) => _labels.Find(b.Label)?.Color ?? b.Color;

    private string LabelsPath => BookmarkLabels.PathOf(_store, _store.Settings.BookmarkCollection);

    /// <summary>Lee las etiquetas de la colección activa (al empezar, al cambiar de colección y tras deshacer).</summary>
    private void LoadLabels()
    {
        _labels = BookmarkLabels.Load(_store, _store.Settings.BookmarkCollection);
        _labelFilter.RemoveWhere(id => id.Length > 0 && _labels.Find(id) == null);
        if (_labels.Labels.Count == 0)
            _labelFilter.Clear();
        BuildLabelChips();
        Seek.InvalidateVisual();
        Zoom.InvalidateVisual();
    }

    private static Brush BrushOf(string hex)
    {
        try { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)); }
        catch (FormatException) { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(BookmarkColors.Default)); }
    }

    // ---- Botoncitos (en el panel y en el cuadro del nombre) ----

    /// <summary>Un botoncito con el punto de color y el nombre; elegido, con fondo de su color. No se lleva el teclado.</summary>
    private Border MakeChip(string text, string? hex, bool selected, string? tip)
    {
        var color = hex != null ? ((SolidColorBrush)BrushOf(hex)).Color : Color.FromRgb(0x9A, 0xA0, 0xA8);
        var content = new StackPanel { Orientation = Orientation.Horizontal };
        if (hex != null)
            content.Children.Add(new Ellipse
            {
                Width = 8, Height = 8, Fill = new SolidColorBrush(color), Margin = new Thickness(0, 0, 5, 0), VerticalAlignment = VerticalAlignment.Center,
            });
        content.Children.Add(new TextBlock
        {
            Text = text, FontSize = 12, VerticalAlignment = VerticalAlignment.Center,
            Foreground = (Brush)FindResource(selected || hex != null ? "Fg" : "FgMuted"),
        });
        var chip = new Border
        {
            Child = content, CornerRadius = new CornerRadius(10), Padding = new Thickness(8, 2, 9, 3), Margin = new Thickness(0, 0, 4, 4),
            BorderThickness = new Thickness(1), Cursor = Cursors.Hand, ToolTip = tip,
            Background = selected ? new SolidColorBrush(Color.FromArgb(0x55, color.R, color.G, color.B)) : new SolidColorBrush(Color.FromRgb(0x22, 0x26, 0x2C)),
            BorderBrush = selected ? new SolidColorBrush(color) : Brushes.Transparent,
        };
        if (!selected)
        {
            chip.MouseEnter += (_, _) => chip.BorderBrush = (Brush)FindResource("Line");
            chip.MouseLeave += (_, _) => chip.BorderBrush = Brushes.Transparent;
        }
        return chip;
    }

    private static string ShortcutOf(int index) => index < 9 ? $"Alt + {index + 1}" : "";

    // Fila de etiquetas del panel: filtran la lista (varias a la vez); clic derecho, su menú; + añade una.
    private void BuildLabelChips()
    {
        if (LabelChips == null)
            return;
        LabelChips.Children.Clear();
        for (var i = 0; i < _labels.Labels.Count; i++)
        {
            var label = _labels.Labels[i];
            var shortcut = ShortcutOf(i);
            var chip = MakeChip(label.Name, label.Color, _labelFilter.Contains(label.Id),
                $"Ver solo los «{label.Name}» (clic en otras para sumarlas). Clic derecho: renombrar, color, borrar…"
                + (shortcut.Length > 0 ? $"\n{shortcut} se la pone al bookmark seleccionado." : ""));
            chip.MouseLeftButtonUp += (_, _) => ToggleLabelFilter(label.Id);
            chip.MouseRightButtonUp += (_, e) =>
            {
                ShowLabelMenu(label, chip);
                e.Handled = true;
            };
            LabelChips.Children.Add(chip);
        }
        if (_labels.Labels.Count > 0)
        {
            var none = MakeChip("Sin etiqueta", null, _labelFilter.Contains(""), "Ver solo los que no tienen etiqueta");
            none.MouseLeftButtonUp += (_, _) => ToggleLabelFilter("");
            LabelChips.Children.Add(none);
        }
        var add = MakeChip(_labels.Labels.Count == 0 ? "+ Etiqueta" : "+", null, false,
            "Nueva etiqueta (por ejemplo «Boss» o «Cinemática»), para saber de un vistazo qué marca cada bookmark");
        add.MouseLeftButtonUp += (_, _) => PromptNewLabel(add, null);
        LabelChips.Children.Add(add);
    }

    private void ToggleLabelFilter(string id)
    {
        if (!_labelFilter.Remove(id))
            _labelFilter.Add(id);
        BuildLabelChips();
        RefreshRows();
    }

    /// <summary>Si la fila pasa el filtro de etiquetas del panel.</summary>
    private bool PassesLabelFilter(Bookmark b) =>
        _labelFilter.Count == 0 || _labelFilter.Contains(_labels.Find(b.Label)?.Id ?? "");

    // Las del cuadro del nombre: una sola (o ninguna); clic en la elegida la quita.
    private void BuildNameChips()
    {
        NameLabels.Children.Clear();
        var show = _naming != null && _labels.Labels.Count > 0;
        NameLabels.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        NameHint.Text = show ? "Enter guarda · Esc lo deja como estaba · Alt + número: etiqueta" : "Enter guarda · Esc deja el nombre como estaba";
        if (!show)
            return;
        for (var i = 0; i < _labels.Labels.Count; i++)
        {
            var label = _labels.Labels[i];
            var chip = MakeChip(label.Name, label.Color, _namingLabel == label.Id, ShortcutOf(i) is { Length: > 0 } s ? s : null);
            chip.MouseLeftButtonUp += (_, _) => ChooseNamingLabel(label.Id);
            NameLabels.Children.Add(chip);
        }
    }

    private void ChooseNamingLabel(string? id)
    {
        _namingLabel = _namingLabel == id ? null : id;
        BuildNameChips();
        Keyboard.Focus(NameBox);
    }

    /// <summary>Alt + 1…9 en el cuadro del nombre (Alt + 0 la quita). Devuelve si era eso.</summary>
    private bool HandleNameLabelKey(KeyEventArgs e)
    {
        if (_naming == null || _labels.Labels.Count == 0 || LabelIndexOf(e) is not { } index)
            return false;
        if (index < 0)
            ChooseNamingLabel(null);
        else if (index < _labels.Labels.Count)
            ChooseNamingLabel(_labels.Labels[index].Id);
        return true;
    }

    /// <summary>Alt + 1…9 → 0…8, Alt + 0 → -1 (quitarla); null si no es eso.</summary>
    private static int? LabelIndexOf(KeyEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.Alt)
            return null;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        var digit = key is >= Key.D0 and <= Key.D9 ? key - Key.D0 : key is >= Key.NumPad0 and <= Key.NumPad9 ? key - Key.NumPad0 : -1;
        return digit < 0 ? null : digit == 0 ? -1 : digit - 1;
    }

    // ---- Poner etiquetas ----

    /// <summary>
    /// Alt + 1…9 (Alt + 0 la quita): a los elegidos en el panel si la lista tiene el teclado, o al seleccionado. Si ya
    /// la llevan, se la quita.
    /// </summary>
    private bool LabelShortcut(KeyEventArgs e)
    {
        if (LabelIndexOf(e) is not { } index)
            return false;
        if (_labels.Labels.Count == 0)
        {
            ShowToast("Esta colección no tiene etiquetas: «+ Etiqueta» en el panel de bookmarks (Ctrl + B)");
            return true;
        }
        if (index >= _labels.Labels.Count)
        {
            ShowToast($"No hay etiqueta {index + 1} (hay {_labels.Labels.Count})");
            return true;
        }
        var label = index < 0 ? null : _labels.Labels[index];
        if (BookmarkList.IsKeyboardFocusWithin && BookmarkList.SelectedItems.Count > 0)
        {
            var rows = BookmarkList.SelectedItems.OfType<BookmarkRow>().ToList();
            var id = label != null && rows.All(r => r.Bookmark.Label == label.Id) ? null : label?.Id;
            SetRowsLabel(rows, id);
            ShowToast(id == null ? "Sin etiqueta" : $"Etiqueta «{label!.Name}»");
        }
        else if (_bookmarks?.Find(_selectedBookmark) is { } bookmark)
        {
            var id = label != null && bookmark.Label == label.Id ? null : label?.Id;
            SetBookmarkLabel(bookmark, id);
            ShowToast(id == null ? $"«{bookmark.Name}» sin etiqueta" : $"«{bookmark.Name}»: {label!.Name}");
        }
        else
        {
            ShowToast("Selecciona antes un bookmark (clic en su marca o en el panel)");
        }
        return true;
    }

    private void SetBookmarkLabel(Bookmark bookmark, string? id)
    {
        if (bookmark.Label == id || _bookmarks == null)
            return;
        _bookmarks.Update(bookmark.Id, b => b.Label = id, label: "etiqueta de");
        RememberLabel(id);
    }

    private void SetRowsLabel(List<BookmarkRow> rows, string? id)
    {
        if (rows.Count == 0)
            return;
        EditRows(rows, rows.Count == 1 ? $"etiqueta de «{rows[0].Name}»" : $"etiqueta de {rows.Count} bookmarks",
            (file, ids) => { foreach (var b in file.Bookmarks.Where(b => ids.Contains(b.Id) && b.Label != id)) { b.Label = id; b.Modified = DateTime.Now; } });
        RememberLabel(id);
    }

    // La última que se puso: la llevan los nuevos (si el ajuste lo pide). No es un paso de deshacer.
    private void RememberLabel(string? id)
    {
        if (id == null || _labels.Last == id)
            return;
        try
        {
            BookmarkLabels.Modify(_store, _store.Settings.BookmarkCollection, f => f.Last = id);
            _labels.Last = id;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
        }
    }

    /// <summary>La etiqueta con la que nace un bookmark: la última que se puso, si el ajuste lo pide y sigue existiendo.</summary>
    private string? NewBookmarkLabel() =>
        _store.Settings.RepeatLastLabel && _labels.Find(_labels.Last) is { } last ? last.Id : null;

    /// <summary>
    /// Submenú "Etiqueta ▸": las de la colección (con su atajo), "Sin etiqueta" y "Nueva etiqueta…". "current" es la que
    /// llevan (null si ninguna o si son varios con distintas).
    /// </summary>
    private void AddLabelMenu(ItemsControl menu, string? current, bool mixed, Action<string?> apply)
    {
        var item = new MenuItem { Header = "Etiqueta" };
        for (var i = 0; i < _labels.Labels.Count; i++)
        {
            var label = _labels.Labels[i];
            var choice = new MenuItem
            {
                Header = label.Name, Icon = Swatch(label.Color), InputGestureText = ShortcutOf(i).Replace(" ", ""),
                IsCheckable = true, IsChecked = !mixed && current == label.Id,
            };
            choice.Click += (_, _) => apply(label.Id);
            item.Items.Add(choice);
        }
        if (_labels.Labels.Count > 0)
        {
            var none = new MenuItem { Header = "Sin etiqueta", InputGestureText = "Alt+0", IsCheckable = true, IsChecked = !mixed && current == null };
            none.Click += (_, _) => apply(null);
            item.Items.Add(none);
            item.Items.Add(new Separator());
        }
        var create = new MenuItem { Header = "Nueva etiqueta…" };
        create.Click += (_, _) => PromptNewLabel(null, apply);
        item.Items.Add(create);
        menu.Items.Add(item);
    }

    private static Rectangle Swatch(string hex) => new()
    {
        Width = 12, Height = 12, RadiusX = 2, RadiusY = 2, Fill = BrushOf(hex),
    };

    // ---- Gestionar las etiquetas ----

    /// <summary>Pide el nombre de una etiqueta nueva (con el primer color libre); "then" se la pone a lo que se estaba editando.</summary>
    private void PromptNewLabel(UIElement? target, Action<string?>? then)
    {
        PromptName("Nombre de la nueva etiqueta", "", target, name =>
        {
            if (_labels.Labels.FirstOrDefault(l => string.Equals(l.Name, name, StringComparison.CurrentCultureIgnoreCase)) is { } existing)
            {
                ShowToast($"Ya hay una etiqueta «{existing.Name}»");
                then?.Invoke(existing.Id);
                return;
            }
            var label = new BookmarkLabel { Id = BookmarkLabels.NewId(), Name = name, Color = BookmarkLabels.NextColor(_labels) };
            if (ChangeLabels($"crear la etiqueta «{name}»", f => f.Labels.Add(label)))
                then?.Invoke(label.Id);
        });
    }

    /// <summary>Cambia las etiquetas de la colección activa (un paso de deshacer, con "extra" si también cambian bookmarks).</summary>
    private bool ChangeLabels(string step, Action<LabelFile> change, List<BookmarkHistory.Entry>? extra = null)
    {
        try
        {
            var (before, after) = BookmarkLabels.Modify(_store, _store.Settings.BookmarkCollection, change);
            _history.Record(step, "", [new(LabelsPath, before, after), .. extra ?? []]);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            ShowToast($"No se pudieron cambiar las etiquetas: {e.Message}");
            return false;
        }
        AfterFilesChanged([LabelsPath, .. (extra ?? []).Select(x => x.Path)]);
        return true;
    }

    /// <summary>Menú de una etiqueta (clic derecho en su botoncito, o en el menú de colecciones).</summary>
    private void ShowLabelMenu(BookmarkLabel label, UIElement target)
    {
        var menu = new ContextMenu { PlacementTarget = target, Placement = PlacementMode.Bottom };
        FillLabelMenu(menu, label);
        menu.IsOpen = true;
    }

    private void FillLabelMenu(ItemsControl menu, BookmarkLabel label)
    {
        var index = _labels.Labels.FindIndex(l => l.Id == label.Id);
        var files = BookmarkSet.LoadAll(_store, _store.Settings.BookmarkCollection).ToList();
        var uses = files.Sum(f => f.Bookmarks.Count(b => b.Label == label.Id));
        if (menu is ContextMenu)
            menu.Items.Add(new MenuItem
            {
                Header = label.Name, IsEnabled = false,
                InputGestureText = uses == 0 ? "sin bookmarks" : uses == 1 ? "1 bookmark" : $"{uses} bookmarks",
            });
        var rename = new MenuItem { Header = "Renombrar…" };
        rename.Click += (_, _) => PromptName($"Nuevo nombre para «{label.Name}»", label.Name, null, name =>
        {
            if (name == label.Name)
                return;
            if (_labels.Labels.Any(l => l.Id != label.Id && string.Equals(l.Name, name, StringComparison.CurrentCultureIgnoreCase)))
            {
                ShowToast($"Ya hay una etiqueta «{name}»");
                return;
            }
            ChangeLabels($"renombrar la etiqueta «{label.Name}»", f => { if (f.Find(label.Id) is { } l) l.Name = name; });
        });
        menu.Items.Add(rename);
        var color = new MenuItem { Header = "Color" };
        AddColorItems(color, label.Color, hex => ChangeLabels($"color de la etiqueta «{label.Name}»", f => { if (f.Find(label.Id) is { } l) l.Color = hex; }), allowOther: true);
        menu.Items.Add(color);
        // El orden es el de Alt + 1…9.
        var left = new MenuItem { Header = "Mover a la izquierda", IsEnabled = index > 0 };
        left.Click += (_, _) => ChangeLabels($"mover la etiqueta «{label.Name}»", f => Swap(f.Labels, index, index - 1));
        menu.Items.Add(left);
        var right = new MenuItem { Header = "Mover a la derecha", IsEnabled = index >= 0 && index < _labels.Labels.Count - 1 };
        right.Click += (_, _) => ChangeLabels($"mover la etiqueta «{label.Name}»", f => Swap(f.Labels, index, index + 1));
        menu.Items.Add(right);
        // Para los bookmarks de antes de las etiquetas, que solo tenían un color.
        var sameColor = files.Sum(f => f.Bookmarks.Count(b => b.Label == null && string.Equals(b.Color, label.Color, StringComparison.OrdinalIgnoreCase)));
        if (sameColor > 0)
        {
            var colorName = BookmarkColors.Palette.FirstOrDefault(p => string.Equals(p.Hex, label.Color, StringComparison.OrdinalIgnoreCase)).Name?.ToLowerInvariant() ?? "de su color";
            var adopt = new MenuItem { Header = $"Ponérsela a los de color {colorName} sin etiqueta ({sameColor})" };
            adopt.Click += (_, _) => EditCollection($"poner la etiqueta «{label.Name}» a {sameColor} bookmarks",
                b => b.Label == null && string.Equals(b.Color, label.Color, StringComparison.OrdinalIgnoreCase), b => b.Label = label.Id, null);
            menu.Items.Add(adopt);
        }
        menu.Items.Add(new Separator());
        var delete = new MenuItem { Header = "Borrar…" };
        delete.Click += (_, _) =>
        {
            if (uses > 0 && !Dialog.Confirm(this,
                    $"¿Borrar la etiqueta «{label.Name}»?\n{(uses == 1 ? "La lleva 1 bookmark: se queda" : $"La llevan {uses} bookmarks: se quedan")} sin etiqueta, con su color.",
                    "Borrar"))
                return;
            // Se quedan con el color que tenían: así no cambia lo que se ve.
            EditCollection($"borrar la etiqueta «{label.Name}»", b => b.Label == label.Id, b => { b.Label = null; b.Color = label.Color; },
                f => { f.Labels.RemoveAll(l => l.Id == label.Id); if (f.Last == label.Id) f.Last = null; });
        };
        menu.Items.Add(delete);
    }

    private static void Swap(List<BookmarkLabel> list, int a, int b)
    {
        if (a < 0 || b < 0 || a >= list.Count || b >= list.Count)
            return;
        (list[a], list[b]) = (list[b], list[a]);
    }

    /// <summary>
    /// Cambia los bookmarks de toda la colección activa que cumplen "match" (y, si se da, sus etiquetas), en un solo paso
    /// de deshacer.
    /// </summary>
    private void EditCollection(string step, Func<Bookmark, bool> match, Action<Bookmark> change, Action<LabelFile>? labels)
    {
        CloseNaming(commit: true);
        var folder = BookmarkCollections.PathOf(_store, _store.Settings.BookmarkCollection);
        var entries = new List<BookmarkHistory.Entry>();
        try
        {
            foreach (var file in BookmarkSet.LoadAll(_store, _store.Settings.BookmarkCollection).Where(f => f.Bookmarks.Any(match)).ToList())
            {
                var path = Path.Combine(folder, file.Fingerprint + ".json");
                var (before, after) = BookmarkFiles.Modify(path, file.Fingerprint, "", f =>
                {
                    foreach (var b in f.Bookmarks.Where(match))
                    {
                        change(b);
                        b.Modified = DateTime.Now;
                    }
                });
                entries.Add(new(path, before, after));
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            ShowToast($"No se pudo cambiar: {e.Message}");
        }
        if (labels != null)
        {
            ChangeLabels(step, labels, entries);
            return;
        }
        _history.Record(step, "", entries);
        AfterFilesChanged(entries.Select(x => x.Path));
    }

    /// <summary>"Etiquetas ▸" en el menú de colecciones: cada una con su menú, y una nueva.</summary>
    private void AddLabelsManageMenu(ItemsControl menu)
    {
        var labels = new MenuItem { Header = "Etiquetas" };
        foreach (var label in _labels.Labels)
        {
            var item = new MenuItem { Header = label.Name, Icon = Swatch(label.Color) };
            // Se rellena al abrirlo: cuenta los bookmarks de la colección.
            item.Items.Add(new MenuItem());
            item.SubmenuOpened += (_, e) =>
            {
                if (e.OriginalSource != item)
                    return;
                item.Items.Clear();
                FillLabelMenu(item, label);
            };
            labels.Items.Add(item);
        }
        if (labels.Items.Count > 0)
            labels.Items.Add(new Separator());
        var create = new MenuItem { Header = "Nueva etiqueta…" };
        create.Click += (_, _) => PromptNewLabel(null, null);
        labels.Items.Add(create);
        menu.Items.Add(labels);
    }

    /// <summary>Nombre de un bookmark con su etiqueta delante, en su color (vista previa).</summary>
    private void SetLabeledName(TextBlock block, Bookmark b)
    {
        block.Inlines.Clear();
        if (_labels.Find(b.Label) is { } label)
            block.Inlines.Add(new Run(label.Name + "  ") { Foreground = BrushOf(label.Color), FontWeight = FontWeights.SemiBold, FontSize = 12 });
        block.Inlines.Add(new Run(b.Name));
    }
}
