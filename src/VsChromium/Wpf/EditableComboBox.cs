// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using VsChromium.Core.Logging;

namespace VsChromium.Wpf {
  public class EditableComboBox : ComboBox {
    public EditableComboBox() {
      IsEditable = true;
      IsTextSearchEnabled = false;
      AddHandler(TextBoxBase.TextChangedEvent, (TextChangedEventHandler)TextBoxOnTextChanged);
      this.Loaded += OnLoaded;
    }

    /// <summary>
    /// OnLoaded is called after OnApplyTemplate for this controls and all its
    /// children.
    /// </summary>
    private void OnLoaded(object sender, RoutedEventArgs routedEventArgs) {
      ApplyCustomBrushes();
    }

    /// <summary>
    /// OnApplyTemplate is called after the control template has been applied to
    /// the control, but not all children may have had their template applied.
    /// </summary>
    public override void OnApplyTemplate() {
      base.OnApplyTemplate();
      ApplyCustomBrushes();
    }

    private void ApplyCustomBrushes() {
      this.Border.Background = this.BorderBackgroundBrush;
      this.DropDownBorder.Background = DropDownBackgroundBrush;
      this.EditableTextBox.Background = this.CursorBrush;
      if (this.ArrowPath != null)
        this.ArrowPath.Fill = this.ArrowBrush;
      if (this.ArrowBorder != null)
        this.ArrowBorder.Background = this.DropDownBackgroundBrush;
    }

    #region TextChanged

    private void TextBoxOnTextChanged(object sender, TextChangedEventArgs e) {
      OnTextChanged(e);
    }

    public event TextChangedEventHandler TextChanged;

    protected virtual void OnTextChanged(TextChangedEventArgs e) {
      TextChangedEventHandler handler = TextChanged;
      if (handler != null)
        handler(this, e);
    }

    #endregion

    #region CursorBrush: Controls the brush of the cursor in the editable text box.

    public static readonly DependencyProperty CursorBrushProperty =
      DependencyProperty.Register("CursorBrush",
                                  typeof(Brush),
                                  typeof(EditableComboBox),
                                  new FrameworkPropertyMetadata(OnCursorBrushPropertyChanged));

    public Brush CursorBrush {
      get {
        return GetValue(CursorBrushProperty) as Brush;
      }
      set {
        SetValue(CursorBrushProperty, value);
      }
    }

    private static void OnCursorBrushPropertyChanged(
      DependencyObject source,
      DependencyPropertyChangedEventArgs e) {
      var control = (EditableComboBox)source;
      var value = (Brush)e.NewValue;
      if (control.EditableTextBox != null)
        control.EditableTextBox.Background = value;
    }

    #endregion

    #region ArrowBrush: Controls the brush of the down arrow to expand to the dropdown.

    public static readonly DependencyProperty ArrowBrushProperty =
      DependencyProperty.Register("ArrowBrush",
                                  typeof(Brush),
                                  typeof(EditableComboBox),
                                  new FrameworkPropertyMetadata(OnArrowBrushPropertyChanged));

    public Brush ArrowBrush {
      get {
        return GetValue(ArrowBrushProperty) as Brush;
      }
      set {
        SetValue(ArrowBrushProperty, value);
      }
    }

    private static void OnArrowBrushPropertyChanged(
      DependencyObject source,
      DependencyPropertyChangedEventArgs e) {
      var control = (EditableComboBox)source;
      var value = (Brush)e.NewValue;
      if (control.ArrowPath != null)
        control.ArrowPath.Fill = value;
    }

    #endregion

    #region DropDownBackgroundBrush: Controls the background brush of the dropdown.

    public static readonly DependencyProperty DropDownBackgroundBrushProperty =
      DependencyProperty.Register("DropDownBackgroundBrush",
                                  typeof(Brush),
                                  typeof(EditableComboBox),
                                  new FrameworkPropertyMetadata(OnDropDownBrushPropertyChanged));

    public Brush DropDownBackgroundBrush {
      get {
        return GetValue(DropDownBackgroundBrushProperty) as Brush;
      }
      set {
        SetValue(DropDownBackgroundBrushProperty, value);
      }
    }

    private static void OnDropDownBrushPropertyChanged(
      DependencyObject source,
      DependencyPropertyChangedEventArgs e) {
      var control = (EditableComboBox)source;
      var value = (Brush)e.NewValue;
      if (control.DropDownBorder != null)
        control.DropDownBorder.Background = value;
    }

    #endregion

    #region BorderBackgroundBrush: Controls the background brush of the editable text box.

    public static readonly DependencyProperty BorderBackgroundBrushProperty =
      DependencyProperty.Register("BorderBackgroundBrush",
                                  typeof(Brush),
                                  typeof(EditableComboBox),
                                  new FrameworkPropertyMetadata(OnBorderBackgroundBrushPropertyChanged));

    public Brush BorderBackgroundBrush {
      get {
        return GetValue(BorderBackgroundBrushProperty) as Brush;
      }
      set {
        SetValue(BorderBackgroundBrushProperty, value);
      }
    }

    private static void OnBorderBackgroundBrushPropertyChanged(
      DependencyObject source,
      DependencyPropertyChangedEventArgs e) {
      var control = (EditableComboBox)source;
      var value = (Brush)e.NewValue;
      if (control.Border != null)
        control.Border.Background = value;
    }

    #endregion

    #region PreviewKeyUp/Down

    public event KeyEventHandler PrePreviewKeyDown;
    public event KeyEventHandler PrePreviewKeyUp;

    protected virtual void OnPrePreviewKeyDown(KeyEventArgs e) {
      KeyEventHandler handler = PrePreviewKeyDown;
      if (handler != null)
        handler(this, e);
    }

    protected virtual void OnPrePreviewKeyUp(KeyEventArgs e) {
      KeyEventHandler handler = PrePreviewKeyUp;
      if (handler != null)
        handler(this, e);
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e) {
      // Update data source first (we don't mark the event as handled)
      if (e.KeyboardDevice.Modifiers == ModifierKeys.None) {
        if (e.Key == Key.Return || e.Key == Key.Enter || e.Key == Key.Up || e.Key == Key.Down) {
          UpdateDataSource();
        }
      }

      // Call our event handlers
      OnPrePreviewKeyDown(e);

      // Call our base behavior is not handled
      if (!e.Handled)
        base.OnPreviewKeyDown(e);
    }

    protected override void OnPreviewKeyUp(KeyEventArgs e) {
      OnPrePreviewKeyUp(e);

      // Call our base behavior is not handled
      if (!e.Handled)
        base.OnPreviewKeyUp(e);
    }

    #endregion

    protected Border Border {
      get {
        return GetTemplateChild("Border") as Border;
      }
    }

    protected TextBox EditableTextBox {
      get {
        return GetTemplateChild("PART_EditableTextBox") as TextBox;
      }
    }

    protected ToggleButton ToggleButton {
      get {
        return GetTemplateChild("toggleButton") as ToggleButton;
      }
    }

    protected Popup DropDownPopup {
      get {
        return GetTemplateChild("PART_Popup") as Popup;
      }
    }

    protected Border DropDownBorder {
      get {
        return GetTemplateChild("DropDownBorder") as Border;
      }
    }

    protected Path ArrowPath {
      get {
        var button = ToggleButton;
        if (button == null)
          return null;
        return button.Template.FindName("Arrow", button) as Path;
      }
    }

    protected Border ArrowBorder {
      get {
        var button = ToggleButton;
        if (button == null)
          return null;
        return button.Template.FindName("templateRoot", button) as Border;
      }
    }

    protected override void OnDropDownOpened(EventArgs e) {
      UpdateDataSource();
      base.OnDropDownOpened(e);
    }

    protected override void OnSelectionChanged(SelectionChangedEventArgs e) {
      base.OnSelectionChanged(e);
      UpdateDataSource();
    }

    /// <summary>
    /// Ensure the text of the editable textbox is present in the combo box
    /// items.
    /// </summary>
    private void UpdateDataSource() {
      BindingExpression expression = GetBindingExpression(ComboBox.TextProperty);
      if (expression != null) {
        expression.UpdateSource();
      }
    }
  }
}
