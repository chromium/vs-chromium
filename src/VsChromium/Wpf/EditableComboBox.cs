// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace VsChromium.Wpf {
  public class EditableComboBox : ComboBox {
    public EditableComboBox() {
      IsEditable = true;
      IsTextSearchEnabled = false;
      AddHandler(TextBoxBase.TextChangedEvent, (TextChangedEventHandler)TextBoxOnTextChanged);
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

    #region CursorBrush

    public static readonly DependencyProperty CursorBrushProperty = DependencyProperty.Register("CursorBrush",
                                                                                                typeof(Brush), typeof(EditableComboBox), new FrameworkPropertyMetadata(OnCursorBrushPropertyChanged));

    public Brush CursorBrush { get { return GetValue(CursorBrushProperty) as Brush; } set { SetValue(CursorBrushProperty, value); } }

    private static void OnCursorBrushPropertyChanged(DependencyObject source, DependencyPropertyChangedEventArgs e) {
      var control = (EditableComboBox)source;
      var value = (Brush)e.NewValue;
      if (control.EditableTextBox != null)
        control.EditableTextBox.Background = value;
    }

    #endregion

    #region ArrowBrush

    public static readonly DependencyProperty ArrowBrushProperty = DependencyProperty.Register("ArrowBrush",
                                                                                               typeof(Brush), typeof(EditableComboBox), new FrameworkPropertyMetadata(OnArrowBrushPropertyChanged));

    public Brush ArrowBrush { get { return GetValue(ArrowBrushProperty) as Brush; } set { SetValue(ArrowBrushProperty, value); } }

    private static void OnArrowBrushPropertyChanged(DependencyObject source, DependencyPropertyChangedEventArgs e) {
      var control = (EditableComboBox)source;
      var value = (Brush)e.NewValue;
      if (control.ArrowPath != null)
        control.ArrowPath.Fill = value;
    }

    #endregion

    #region DropDownBrush

    public static readonly DependencyProperty DropDownBrushProperty = DependencyProperty.Register("DropDownBrush",
                                                                                                  typeof(Brush), typeof(EditableComboBox), new FrameworkPropertyMetadata(OnDropDownBrushPropertyChanged));

    public Brush DropDownBrush { get { return GetValue(DropDownBrushProperty) as Brush; } set { SetValue(DropDownBrushProperty, value); } }

    private static void OnDropDownBrushPropertyChanged(DependencyObject source, DependencyPropertyChangedEventArgs e) {
      var control = (EditableComboBox)source;
      var value = (Brush)e.NewValue;
      if (control.DropDownBorder != null)
        control.DropDownBorder.Background = value;
    }

    #endregion

    #region PreviewKeyDown

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

    protected TextBox EditableTextBox { get { return GetTemplateChild("PART_EditableTextBox") as TextBox; } }

    protected ToggleButton ToggleButton { get { return GetTemplateChild("5_T") as ToggleButton; } }

    protected Popup DropDownPopup { get { return GetTemplateChild("PART_Popup") as Popup; } }

    protected Border DropDownBorder { get { return GetTemplateChild("DropDownBorder") as Border; } }

    protected Path ArrowPath {
      get {
        var button = ToggleButton;
        if (button == null)
          return null;
        return button.Template.FindName("Arrow", button) as Path;
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

    private void UpdateDataSource() {
      BindingExpression expression = GetBindingExpression(ComboBox.TextProperty);
      if (expression != null) {
        expression.UpdateSource();
      }
    }

    public override void EndInit() {
      base.EndInit();
      Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs routedEventArgs) {
      if (CursorBrush != null)
        OnCursorBrushPropertyChanged(this,
                                     new DependencyPropertyChangedEventArgs(CursorBrushProperty, null, CursorBrush));
      if (ArrowBrush != null)
        OnArrowBrushPropertyChanged(this, new DependencyPropertyChangedEventArgs(ArrowBrushProperty, null, ArrowBrush));
      if (DropDownBrush != null)
        OnDropDownBrushPropertyChanged(this,
                                       new DependencyPropertyChangedEventArgs(DropDownBrushProperty, null, DropDownBrush));
    }
  }
}
