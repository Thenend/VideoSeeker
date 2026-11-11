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
    // Etiqueta y color propio elegidos en el cuadro del nombre (se guardan con el nombre al aceptarlo). O etiqueta o
    // color: con etiqueta, manda su color.
    private string? _namingLabel;
    private string _namingColor = BookmarkColors.Default;
    // Se creó una etiqueta con el cuadro abierto: ya no es el último paso de deshacer el de crear el bookmark.
    private bool _namingCreatedLabel;

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
                S.LabelChipTip(label.Name) + (shortcut.Length > 0 ? S.LabelChipShortcut(shortcut) : ""));
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
            var none = MakeChip(S.NoLabel, null, _labelFilter.Contains(""), S.NoLabelChipTip);
            none.MouseLeftButtonUp += (_, _) => ToggleLabelFilter("");
            LabelChips.Children.Add(none);
        }
        var add = MakeChip(_labels.Labels.Count == 0 ? S.AddLabelChip : "+", null, false, S.AddLabelChipTip);
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

    // Las del cuadro del nombre de un bookmark: sus etiquetas, «+ Nueva» y, debajo, los colores sueltos. O etiqueta o color:
    // lo activo se ve siempre marcado (pincharlo otra vez no hace nada) y se aplica al guardar. Elegir un color es la
    // manera de quitar la etiqueta. Con el teclado: Tab (o las flechas) los recorre, Espacio elige el que tiene el foco e
    // Intro lo elige y guarda; en «+ Nueva», Espacio o Intro abren el campo.
    private void BuildNameChips()
    {
        // Si el foco estaba en uno (se eligió con Espacio), vuelve al mismo tras rehacerlos.
        var focused = Keyboard.FocusedElement is Border { Tag: string key } b && (IsInside(b, NameLabels) || IsInside(b, NameColors)) ? key : null;
        NameLabels.Children.Clear();
        NameColors.Children.Clear();
        var show = _naming != null;
        NameLabels.Visibility = NameColors.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        if (!show)
            NewLabelRow.Visibility = Visibility.Collapsed;
        NameHint.Text = show ? S.NameHintWithLabels : S.NameHint;
        if (!show)
            return;
        for (var i = 0; i < _labels.Labels.Count; i++)
        {
            var label = _labels.Labels[i];
            var chip = MakeChip(label.Name, label.Color, _namingLabel == label.Id, ShortcutOf(i) is { Length: > 0 } s ? s : null);
            chip.MouseLeftButtonUp += (_, _) => ChooseNamingLabel(label.Id);
            NameLabels.Children.Add(KeyboardRing(chip, "label:" + label.Id, new CornerRadius(11), () => ChooseNamingLabel(label.Id, keepFocus: true), closes: true));
        }
        var add = MakeChip(_labels.Labels.Count == 0 ? S.AddLabelChipLong : S.AddLabelChipShort, null, false, S.AddLabelChipNameTip);
        add.MouseLeftButtonUp += (_, _) => ShowNewLabelRow();
        NameLabels.Children.Add(KeyboardRing(add, "add", new CornerRadius(11), ShowNewLabelRow, closes: false));
        foreach (var (name, hex) in BookmarkColors.Palette)
        {
            var swatch = MakeColorSwatch(hex, _namingLabel == null && string.Equals(_namingColor, hex, StringComparison.OrdinalIgnoreCase), S.ColorWithoutLabel(name));
            swatch.MouseLeftButtonUp += (_, _) => ChooseNamingColor(hex);
            NameColors.Children.Add(KeyboardRing(swatch, "color:" + hex, new CornerRadius(10), () => ChooseNamingColor(hex, keepFocus: true), closes: true));
        }
        if (focused != null
            && NameLabels.Children.Cast<UIElement>().Concat(NameColors.Children.Cast<UIElement>()).FirstOrDefault(c => c is Border { Tag: string k } && k == focused) is { } again)
            Dispatcher.BeginInvoke(() =>
            {
                if (NamePopup.IsOpen)
                    Keyboard.Focus(again);
            }, System.Windows.Threading.DispatcherPriority.Input);
    }

    /// <summary>
    /// Envuelve un botoncito o un color del cuadro del nombre para que se pueda usar con el teclado: entra en el orden de Tab,
    /// con el foco se le ve un anillo del color de acento (fuera de su propio borde, que ya marca si está elegido), Espacio
    /// hace lo suyo e Intro, además, guarda y cierra el cuadro si "closes".
    /// </summary>
    private Border KeyboardRing(FrameworkElement inner, string key, CornerRadius radius, Action activate, bool closes)
    {
        inner.Margin = new Thickness(0);
        var ring = new Border
        {
            Child = inner, Tag = key, CornerRadius = radius, BorderThickness = new Thickness(1), BorderBrush = Brushes.Transparent,
            Margin = new Thickness(0, 0, 3, 2), Focusable = true, FocusVisualStyle = null,
        };
        ring.GotKeyboardFocus += (_, _) => ring.BorderBrush = (Brush)FindResource("Accent");
        ring.LostKeyboardFocus += (_, _) => ring.BorderBrush = Brushes.Transparent;
        ring.KeyDown += (_, e) =>
        {
            if (Keyboard.Modifiers != ModifierKeys.None)
                return;
            if (e.Key == Key.Space)
                activate();
            else if (e.Key == Key.Enter)
            {
                activate();
                if (closes)
                    CloseNaming(commit: true);
            }
            else
                return;
            e.Handled = true;
        };
        return ring;
    }

    /// <summary>Muestra de un color suelto para el cuadro del nombre; la elegida, con un anillo claro.</summary>
    private Border MakeColorSwatch(string hex, bool selected, string tip)
    {
        var swatch = new Border
        {
            Width = 18, Height = 18, CornerRadius = new CornerRadius(9), Margin = new Thickness(0, 0, 5, 2), Cursor = Cursors.Hand, ToolTip = tip,
            Background = BrushOf(hex), BorderThickness = new Thickness(2),
            BorderBrush = selected ? (Brush)FindResource("Fg") : Brushes.Transparent,
        };
        if (!selected)
        {
            swatch.MouseEnter += (_, _) => swatch.BorderBrush = (Brush)FindResource("Line");
            swatch.MouseLeave += (_, _) => swatch.BorderBrush = Brushes.Transparent;
        }
        return swatch;
    }

    // Con el ratón, el foco vuelve al nombre; con el teclado (keepFocus), se queda en lo elegido.
    private void ChooseNamingLabel(string id, bool keepFocus = false)
    {
        _namingLabel = id;
        BuildNameChips();
        if (!keepFocus)
            Keyboard.Focus(NameBox);
    }

    // Un color suelto: sin etiqueta y con ese color.
    private void ChooseNamingColor(string hex, bool keepFocus = false)
    {
        _namingLabel = null;
        _namingColor = hex;
        BuildNameChips();
        if (!keepFocus)
            Keyboard.Focus(NameBox);
    }

    // «+ Nueva»: un campo dentro del mismo cuadro para el nombre de la etiqueta.
    private void ShowNewLabelRow()
    {
        NewLabelBox.Text = "";
        NewLabelRow.Visibility = Visibility.Visible;
        Dispatcher.BeginInvoke(() => Keyboard.Focus(NewLabelBox), System.Windows.Threading.DispatcherPriority.Input);
    }

    private void HideNewLabelRow()
    {
        NewLabelRow.Visibility = Visibility.Collapsed;
        Keyboard.Focus(NameBox);
    }

    // Enter crea la etiqueta (con el primer color libre) y la elige; Esc vuelve al nombre del bookmark.
    private void OnNewLabelBoxKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter:
                CreateNamingLabel(NewLabelBox.Text.Trim());
                e.Handled = true;
                break;
            case Key.Escape:
                HideNewLabelRow();
                e.Handled = true;
                break;
        }
    }

    /// <summary>Crea (o reutiliza, si ya hay una con ese nombre) una etiqueta desde el cuadro del nombre y la elige.</summary>
    private void CreateNamingLabel(string name)
    {
        if (name.Length == 0)
        {
            HideNewLabelRow();
            return;
        }
        var existing = _labels.Labels.FirstOrDefault(l => string.Equals(l.Name, name, StringComparison.CurrentCultureIgnoreCase));
        var label = existing ?? new BookmarkLabel { Id = BookmarkLabels.NewId(), Name = name, Color = BookmarkLabels.NextColor(_labels) };
        if (existing == null)
        {
            if (!ChangeLabels(S.StepCreateLabel(name), f => f.Labels.Add(label)))
                return;
            _namingCreatedLabel = true;
        }
        NewLabelRow.Visibility = Visibility.Collapsed;
        ChooseNamingLabel(label.Id);
    }

    /// <summary>Alt + 1…9 en el cuadro del nombre. Devuelve si era eso (Alt + 0 no hace nada).</summary>
    private bool HandleNameLabelKey(KeyEventArgs e)
    {
        if (_naming == null || LabelIndexOf(e) is not { } index)
            return false;
        if (index >= 0 && index < _labels.Labels.Count)
            ChooseNamingLabel(_labels.Labels[index].Id);
        return true;
    }

    /// <summary>Alt + 1…9 → 0…8, Alt + 0 → -1 (no es ninguna); null si no es eso.</summary>
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
    /// Alt + 1…9: esa etiqueta a los elegidos en el panel si la lista tiene el teclado, o al seleccionado (sustituye su
    /// color). Para quitarla se elige un color (menú o cuadro del nombre).
    /// </summary>
    private bool LabelShortcut(KeyEventArgs e)
    {
        if (LabelIndexOf(e) is not { } index)
            return false;
        if (index < 0)
            return true;
        if (_labels.Labels.Count == 0)
        {
            ShowToast(S.NoLabelsInCollection);
            return true;
        }
        if (index >= _labels.Labels.Count)
        {
            ShowToast(S.NoLabelNumber(index + 1, _labels.Labels.Count));
            return true;
        }
        var label = _labels.Labels[index];
        if (BookmarkList.IsKeyboardFocusWithin && BookmarkList.SelectedItems.Count > 0)
        {
            SetRowsLabel(BookmarkList.SelectedItems.OfType<BookmarkRow>().ToList(), label.Id);
            ShowToast(S.LabelToast(label.Name));
        }
        else if (_bookmarks?.Find(_selectedBookmark) is { } bookmark)
        {
            SetBookmarkLabel(bookmark, label.Id);
            ShowToast(S.BookmarkWithLabel(bookmark.Name, label.Name));
        }
        else
        {
            ShowToast(S.SelectBookmarkFirst);
        }
        return true;
    }

    private void SetBookmarkLabel(Bookmark bookmark, string? id)
    {
        if (bookmark.Label == id || _bookmarks == null)
            return;
        _bookmarks.Update(bookmark.Id, b => b.Label = id, label: S.StepLabelOf);
        RememberChoice(id, bookmark.Color);
    }

    // Un color suelto: le quita la etiqueta y le pone ese color, en un solo paso de deshacer.
    private void SetBookmarkColor(Bookmark bookmark, string hex)
    {
        if (_bookmarks == null || (bookmark.Label == null && string.Equals(bookmark.Color, hex, StringComparison.OrdinalIgnoreCase)))
            return;
        _bookmarks.Update(bookmark.Id, b => { b.Label = null; b.Color = hex; }, label: S.StepColorOf);
        RememberChoice(null, hex);
    }

    private void SetRowsLabel(List<BookmarkRow> rows, string? id)
    {
        if (rows.Count == 0)
            return;
        EditRows(rows, rows.Count == 1 ? S.StepLabelOf(rows[0].Name) : S.StepLabelOfMany(rows.Count),
            (file, ids) => { foreach (var b in file.Bookmarks.Where(b => ids.Contains(b.Id) && b.Label != id)) { b.Label = id; b.Modified = DateTime.Now; } });
        RememberChoice(id, rows.Select(r => r.Bookmark.Color).Distinct().Count() == 1 ? rows[0].Bookmark.Color : null);
    }

    private void SetRowsColor(List<BookmarkRow> rows, string hex)
    {
        if (rows.Count == 0)
            return;
        EditRows(rows, rows.Count == 1 ? S.StepColorOf(rows[0].Name) : S.StepColorOfMany(rows.Count),
            (file, ids) => { foreach (var b in file.Bookmarks.Where(b => ids.Contains(b.Id) && (b.Label != null || b.Color != hex))) { b.Label = null; b.Color = hex; b.Modified = DateTime.Now; } });
        RememberChoice(null, hex);
    }

    /// <summary>
    /// Lo último que se eligió, etiqueta o color suelto: lo llevan los nuevos (si el ajuste lo pide). Sin etiqueta y sin
    /// color conocido (varios con colores distintos), se queda el último color. No es un paso de deshacer.
    /// </summary>
    private void RememberChoice(string? label, string? color)
    {
        var lastColor = label != null ? null : color ?? _labels.LastColor;
        if (_labels.Last == label && _labels.LastColor == lastColor)
            return;
        try
        {
            BookmarkLabels.Modify(_store, _store.Settings.BookmarkCollection, f => { f.Last = label; f.LastColor = lastColor; });
            _labels.Last = label;
            _labels.LastColor = lastColor;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
        }
    }

    /// <summary>La etiqueta con la que nace un bookmark: la última que se eligió, si el ajuste lo pide y sigue existiendo.</summary>
    private string? NewBookmarkLabel() =>
        _store.Settings.RepeatLastLabel && _labels.Find(_labels.Last) is { } last ? last.Id : null;

    /// <summary>
    /// El color propio con el que nace: si lo último que se eligió fue un color suelto (y el ajuste pide repetirlo), ese;
    /// si no, el de los nuevos de los ajustes.
    /// </summary>
    private string NewBookmarkColor() =>
        _store.Settings.RepeatLastLabel && NewBookmarkLabel() == null && _labels.LastColor is { } hex
            && BookmarkColors.Palette.Any(p => string.Equals(p.Hex, hex, StringComparison.OrdinalIgnoreCase))
            ? hex : _store.Settings.NewBookmarkColor;

    /// <summary>
    /// Submenú "Etiqueta o color ▸": las etiquetas de la colección (con su atajo), los colores sueltos y
    /// "Nueva etiqueta…". O etiqueta o color: elegir un color quita la etiqueta. "label" y "color" son lo que llevan
    /// (null si son varios con distintos).
    /// </summary>
    private void AddLabelOrColorMenu(ItemsControl menu, string? label, string? color, bool mixedLabels, Action<string?> applyLabel, Action<string> applyColor)
    {
        var item = new MenuItem { Header = S.LabelOrColor };
        for (var i = 0; i < _labels.Labels.Count; i++)
        {
            var l = _labels.Labels[i];
            var choice = new MenuItem
            {
                Header = l.Name, Icon = Swatch(l.Color), InputGestureText = ShortcutOf(i).Replace(" ", ""),
                IsCheckable = true, IsChecked = !mixedLabels && label == l.Id,
            };
            choice.Click += (_, _) => applyLabel(l.Id);
            item.Items.Add(choice);
        }
        if (_labels.Labels.Count > 0)
            item.Items.Add(new Separator());
        foreach (var (name, hex) in BookmarkColors.Palette)
        {
            var choice = new MenuItem
            {
                Header = name, Icon = Swatch(hex), IsCheckable = true,
                IsChecked = !mixedLabels && label == null && string.Equals(color, hex, StringComparison.OrdinalIgnoreCase),
            };
            choice.Click += (_, _) => applyColor(hex);
            item.Items.Add(choice);
        }
        item.Items.Add(new Separator());
        var create = new MenuItem { Header = S.NewLabel };
        create.Click += (_, _) => PromptNewLabel(null, applyLabel);
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
        PromptName(S.NewLabelName, "", target, name =>
        {
            if (_labels.Labels.FirstOrDefault(l => string.Equals(l.Name, name, StringComparison.CurrentCultureIgnoreCase)) is { } existing)
            {
                ShowToast(S.LabelExists(existing.Name));
                then?.Invoke(existing.Id);
                return;
            }
            var label = new BookmarkLabel { Id = BookmarkLabels.NewId(), Name = name, Color = BookmarkLabels.NextColor(_labels) };
            if (ChangeLabels(S.StepCreateLabel(name), f => f.Labels.Add(label)))
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
            ShowToast(S.LabelsChangeFailed(e.Message));
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
                InputGestureText = uses == 0 ? S.NoBookmarks : S.BookmarkCount(uses),
            });
        var rename = new MenuItem { Header = S.RenameEllipsis };
        rename.Click += (_, _) => PromptName(S.NewNameFor(label.Name), label.Name, null, name =>
        {
            if (name == label.Name)
                return;
            if (_labels.Labels.Any(l => l.Id != label.Id && string.Equals(l.Name, name, StringComparison.CurrentCultureIgnoreCase)))
            {
                ShowToast(S.LabelExists(name));
                return;
            }
            ChangeLabels(S.StepRenameLabel(label.Name), f => { if (f.Find(label.Id) is { } l) l.Name = name; });
        });
        menu.Items.Add(rename);
        var color = new MenuItem { Header = S.Color };
        AddColorItems(color, label.Color, hex => ChangeLabels(S.StepLabelColor(label.Name), f => { if (f.Find(label.Id) is { } l) l.Color = hex; }), allowOther: true);
        menu.Items.Add(color);
        // El orden es el de Alt + 1…9.
        var left = new MenuItem { Header = S.MoveLeft, IsEnabled = index > 0 };
        left.Click += (_, _) => ChangeLabels(S.StepMoveLabel(label.Name), f => Swap(f.Labels, index, index - 1));
        menu.Items.Add(left);
        var right = new MenuItem { Header = S.MoveRight, IsEnabled = index >= 0 && index < _labels.Labels.Count - 1 };
        right.Click += (_, _) => ChangeLabels(S.StepMoveLabel(label.Name), f => Swap(f.Labels, index, index + 1));
        menu.Items.Add(right);
        // Para los bookmarks de antes de las etiquetas, que solo tenían un color.
        var sameColor = files.Sum(f => f.Bookmarks.Count(b => b.Label == null && string.Equals(b.Color, label.Color, StringComparison.OrdinalIgnoreCase)));
        if (sameColor > 0)
        {
            var colorName = BookmarkColors.Palette.FirstOrDefault(p => string.Equals(p.Hex, label.Color, StringComparison.OrdinalIgnoreCase)).Name;
            var adopt = new MenuItem { Header = colorName != null ? S.AdoptLabelColor(colorName, sameColor) : S.AdoptLabelSameColor(sameColor) };
            adopt.Click += (_, _) => EditCollection(S.StepAdoptLabel(label.Name, sameColor),
                b => b.Label == null && string.Equals(b.Color, label.Color, StringComparison.OrdinalIgnoreCase), b => b.Label = label.Id, null);
            menu.Items.Add(adopt);
        }
        menu.Items.Add(new Separator());
        var delete = new MenuItem { Header = S.DeleteEllipsis };
        delete.Click += (_, _) =>
        {
            if (uses > 0 && !Dialog.Confirm(this,
                    S.DeleteLabelQuestion(label.Name, uses), S.Delete))
                return;
            // Se quedan con el color que tenían: así no cambia lo que se ve.
            EditCollection(S.StepDeleteLabel(label.Name), b => b.Label == label.Id, b => { b.Label = null; b.Color = label.Color; },
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
            ShowToast(S.ChangeFailed(e.Message));
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
        var labels = new MenuItem { Header = S.Labels };
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
        var create = new MenuItem { Header = S.NewLabel };
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
