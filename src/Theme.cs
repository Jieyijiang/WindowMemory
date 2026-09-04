using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Markup;
using System.Xml;

namespace WindowMemory
{
    public static class Theme
    {
        public static ResourceDictionary Create()
        {
            const string xaml = @"
<ResourceDictionary xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
                    xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'>
  <Color x:Key='BackgroundColor'>#0B1220</Color>
  <Color x:Key='SidebarColor'>#0E1728</Color>
  <Color x:Key='SurfaceColor'>#121D31</Color>
  <Color x:Key='SurfaceRaisedColor'>#17243A</Color>
  <Color x:Key='SurfaceHoverColor'>#1B2A43</Color>
  <Color x:Key='AccentColor'>#15B981</Color>
  <Color x:Key='AccentHoverColor'>#1BC991</Color>
  <Color x:Key='AccentPressedColor'>#0EA371</Color>
  <Color x:Key='TextColor'>#F8FAFC</Color>
  <Color x:Key='MutedTextColor'>#9AABC1</Color>
  <Color x:Key='SubtleTextColor'>#71839B</Color>
  <Color x:Key='BorderColor'>#263650</Color>
  <Color x:Key='FocusColor'>#59E3B4</Color>
  <Color x:Key='DangerColor'>#E45858</Color>
  <Color x:Key='WarningColor'>#F5B942</Color>

  <SolidColorBrush x:Key='BackgroundBrush' Color='{StaticResource BackgroundColor}'/>
  <SolidColorBrush x:Key='SidebarBrush' Color='{StaticResource SidebarColor}'/>
  <SolidColorBrush x:Key='SurfaceBrush' Color='{StaticResource SurfaceColor}'/>
  <SolidColorBrush x:Key='SurfaceRaisedBrush' Color='{StaticResource SurfaceRaisedColor}'/>
  <SolidColorBrush x:Key='SurfaceHoverBrush' Color='{StaticResource SurfaceHoverColor}'/>
  <SolidColorBrush x:Key='AccentBrush' Color='{StaticResource AccentColor}'/>
  <SolidColorBrush x:Key='AccentHoverBrush' Color='{StaticResource AccentHoverColor}'/>
  <SolidColorBrush x:Key='AccentPressedBrush' Color='{StaticResource AccentPressedColor}'/>
  <SolidColorBrush x:Key='TextBrush' Color='{StaticResource TextColor}'/>
  <SolidColorBrush x:Key='MutedTextBrush' Color='{StaticResource MutedTextColor}'/>
  <SolidColorBrush x:Key='SubtleTextBrush' Color='{StaticResource SubtleTextColor}'/>
  <SolidColorBrush x:Key='BorderBrush' Color='{StaticResource BorderColor}'/>
  <SolidColorBrush x:Key='FocusBrush' Color='{StaticResource FocusColor}'/>
  <SolidColorBrush x:Key='DangerBrush' Color='{StaticResource DangerColor}'/>
  <SolidColorBrush x:Key='WarningBrush' Color='{StaticResource WarningColor}'/>

  <FontFamily x:Key='AppFont'>Segoe UI Variable Text, Segoe UI</FontFamily>

  <Style TargetType='Window'>
    <Setter Property='Background' Value='{StaticResource BackgroundBrush}'/>
    <Setter Property='Foreground' Value='{StaticResource TextBrush}'/>
    <Setter Property='FontFamily' Value='{StaticResource AppFont}'/>
    <Setter Property='FontSize' Value='14'/>
    <Setter Property='TextOptions.TextFormattingMode' Value='Display'/>
    <Setter Property='TextOptions.TextRenderingMode' Value='ClearType'/>
  </Style>

  <Style x:Key='Card' TargetType='Border'>
    <Setter Property='Background' Value='{StaticResource SurfaceBrush}'/>
    <Setter Property='BorderBrush' Value='{StaticResource BorderBrush}'/>
    <Setter Property='BorderThickness' Value='1'/>
    <Setter Property='CornerRadius' Value='14'/>
    <Setter Property='Padding' Value='20'/>
    <Setter Property='SnapsToDevicePixels' Value='True'/>
    <Setter Property='Effect'>
      <Setter.Value><DropShadowEffect Color='#000000' BlurRadius='18' ShadowDepth='4' Opacity='0.18'/></Setter.Value>
    </Setter>
  </Style>

  <Style TargetType='Button'>
    <Setter Property='Background' Value='{StaticResource SurfaceRaisedBrush}'/>
    <Setter Property='Foreground' Value='{StaticResource TextBrush}'/>
    <Setter Property='BorderBrush' Value='{StaticResource BorderBrush}'/>
    <Setter Property='BorderThickness' Value='1'/>
    <Setter Property='Padding' Value='16,0'/>
    <Setter Property='MinHeight' Value='40'/>
    <Setter Property='MinWidth' Value='76'/>
    <Setter Property='FontFamily' Value='{StaticResource AppFont}'/>
    <Setter Property='FontSize' Value='13'/>
    <Setter Property='FontWeight' Value='SemiBold'/>
    <Setter Property='Cursor' Value='Hand'/>
    <Setter Property='FocusVisualStyle' Value='{x:Null}'/>
    <Setter Property='Template'>
      <Setter.Value>
        <ControlTemplate TargetType='Button'>
          <Border x:Name='Root' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}'
                  BorderThickness='{TemplateBinding BorderThickness}' CornerRadius='9' RenderTransformOrigin='0.5,0.5'
                  SnapsToDevicePixels='True'>
            <Border.RenderTransform><ScaleTransform ScaleX='1' ScaleY='1'/></Border.RenderTransform>
            <ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center' Margin='{TemplateBinding Padding}'
                              RecognizesAccessKey='True'/>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property='IsPressed' Value='True'>
              <Setter TargetName='Root' Property='RenderTransform'><Setter.Value><ScaleTransform ScaleX='0.96' ScaleY='0.96'/></Setter.Value></Setter>
              <Setter TargetName='Root' Property='Opacity' Value='0.88'/>
            </Trigger>
            <Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='Root' Property='BorderBrush' Value='{StaticResource FocusBrush}'/><Setter TargetName='Root' Property='BorderThickness' Value='2'/></Trigger>
            <Trigger Property='IsEnabled' Value='False'><Setter TargetName='Root' Property='Opacity' Value='0.4'/></Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
    <Style.Triggers>
      <Trigger Property='IsMouseOver' Value='True'><Setter Property='Background' Value='{StaticResource SurfaceHoverBrush}'/></Trigger>
    </Style.Triggers>
  </Style>

  <Style x:Key='PrimaryButton' TargetType='Button' BasedOn='{StaticResource {x:Type Button}}'>
    <Setter Property='Background' Value='{StaticResource AccentBrush}'/>
    <Setter Property='BorderBrush' Value='{StaticResource AccentBrush}'/>
    <Setter Property='Foreground' Value='White'/>
    <Style.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter Property='Background' Value='{StaticResource AccentHoverBrush}'/><Setter Property='BorderBrush' Value='{StaticResource AccentHoverBrush}'/></Trigger></Style.Triggers>
  </Style>

  <Style x:Key='DangerButton' TargetType='Button' BasedOn='{StaticResource {x:Type Button}}'>
    <Setter Property='Foreground' Value='#FF9C9C'/>
  </Style>

  <Style x:Key='NavButton' TargetType='Button' BasedOn='{StaticResource {x:Type Button}}'>
    <Setter Property='HorizontalContentAlignment' Value='Left'/>
    <Setter Property='Background' Value='Transparent'/>
    <Setter Property='BorderBrush' Value='Transparent'/>
    <Setter Property='Foreground' Value='{StaticResource MutedTextBrush}'/>
    <Setter Property='Padding' Value='16,0'/>
    <Setter Property='Margin' Value='0,3'/>
  </Style>

  <Style TargetType='TextBox'>
    <Setter Property='Background' Value='{StaticResource SurfaceRaisedBrush}'/>
    <Setter Property='Foreground' Value='{StaticResource TextBrush}'/>
    <Setter Property='CaretBrush' Value='{StaticResource TextBrush}'/>
    <Setter Property='BorderBrush' Value='{StaticResource BorderBrush}'/>
    <Setter Property='BorderThickness' Value='1'/>
    <Setter Property='Padding' Value='12,9'/>
    <Setter Property='MinHeight' Value='40'/>
    <Setter Property='FontSize' Value='14'/>
    <Setter Property='VerticalContentAlignment' Value='Center'/>
    <Setter Property='FocusVisualStyle' Value='{x:Null}'/>
    <Style.Triggers><Trigger Property='IsKeyboardFocused' Value='True'><Setter Property='BorderBrush' Value='{StaticResource FocusBrush}'/><Setter Property='BorderThickness' Value='2'/></Trigger></Style.Triggers>
  </Style>

  <Style TargetType='ComboBox'>
    <Setter Property='Background' Value='{StaticResource SurfaceRaisedBrush}'/>
    <Setter Property='Foreground' Value='{StaticResource TextBrush}'/>
    <Setter Property='BorderBrush' Value='{StaticResource BorderBrush}'/>
    <Setter Property='BorderThickness' Value='1'/>
    <Setter Property='Padding' Value='10,7'/>
    <Setter Property='MinHeight' Value='40'/>
    <Setter Property='FocusVisualStyle' Value='{x:Null}'/>
    <Setter Property='Template'>
      <Setter.Value>
        <ControlTemplate TargetType='ComboBox'>
          <Grid>
            <ToggleButton x:Name='Toggle' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}'
                          BorderThickness='{TemplateBinding BorderThickness}' Focusable='False' ClickMode='Press'
                          IsChecked='{Binding IsDropDownOpen, Mode=TwoWay, RelativeSource={RelativeSource TemplatedParent}}'>
              <ToggleButton.Template>
                <ControlTemplate TargetType='ToggleButton'>
                  <Border x:Name='Shell' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}'
                          BorderThickness='{TemplateBinding BorderThickness}' CornerRadius='9'>
                    <Grid>
                      <Grid.ColumnDefinitions><ColumnDefinition/><ColumnDefinition Width='34'/></Grid.ColumnDefinitions>
                      <ContentPresenter Margin='12,0,4,0' VerticalAlignment='Center' HorizontalAlignment='Left'/>
                      <Path Grid.Column='1' Data='M 0 0 L 4 4 L 8 0' Stroke='{StaticResource MutedTextBrush}'
                            StrokeThickness='1.5' HorizontalAlignment='Center' VerticalAlignment='Center'/>
                    </Grid>
                  </Border>
                  <ControlTemplate.Triggers>
                    <Trigger Property='IsMouseOver' Value='True'><Setter TargetName='Shell' Property='Background' Value='{StaticResource SurfaceHoverBrush}'/></Trigger>
                  </ControlTemplate.Triggers>
                </ControlTemplate>
              </ToggleButton.Template>
            </ToggleButton>
            <ContentPresenter IsHitTestVisible='False' Content='{TemplateBinding SelectionBoxItem}'
                              ContentTemplate='{TemplateBinding SelectionBoxItemTemplate}' Margin='12,0,34,0'
                              VerticalAlignment='Center' HorizontalAlignment='Left'/>
            <Popup x:Name='Popup' Placement='Bottom' IsOpen='{TemplateBinding IsDropDownOpen}' AllowsTransparency='True' Focusable='False'>
              <Border Background='{StaticResource SurfaceRaisedBrush}' BorderBrush='{StaticResource BorderBrush}' BorderThickness='1'
                      CornerRadius='9' Margin='0,4,0,0' MinWidth='{TemplateBinding ActualWidth}' MaxHeight='260'>
                <ScrollViewer Margin='4' SnapsToDevicePixels='True'><ItemsPresenter/></ScrollViewer>
              </Border>
            </Popup>
          </Grid>
          <ControlTemplate.Triggers>
            <Trigger Property='IsKeyboardFocusWithin' Value='True'><Setter TargetName='Toggle' Property='BorderBrush' Value='{StaticResource FocusBrush}'/></Trigger>
            <Trigger Property='IsEnabled' Value='False'><Setter Property='Opacity' Value='0.4'/></Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style TargetType='ComboBoxItem'>
    <Setter Property='Foreground' Value='{StaticResource TextBrush}'/>
    <Setter Property='Background' Value='Transparent'/>
    <Setter Property='Padding' Value='10,8'/>
    <Setter Property='HorizontalContentAlignment' Value='Stretch'/>
    <Style.Triggers>
      <Trigger Property='IsHighlighted' Value='True'><Setter Property='Background' Value='{StaticResource SurfaceHoverBrush}'/></Trigger>
      <Trigger Property='IsSelected' Value='True'><Setter Property='Background' Value='#253E5C'/></Trigger>
    </Style.Triggers>
  </Style>

  <Style TargetType='CheckBox'>
    <Setter Property='Foreground' Value='{StaticResource TextBrush}'/>
    <Setter Property='FontSize' Value='13'/>
    <Setter Property='MinHeight' Value='32'/>
    <Setter Property='VerticalContentAlignment' Value='Center'/>
    <Setter Property='FocusVisualStyle' Value='{x:Null}'/>
    <Setter Property='Template'>
      <Setter.Value>
        <ControlTemplate TargetType='CheckBox'>
          <StackPanel Orientation='Horizontal' VerticalAlignment='Center'>
            <Border x:Name='Box' Width='18' Height='18' CornerRadius='4' Background='{StaticResource SurfaceRaisedBrush}'
                    BorderBrush='{StaticResource BorderBrush}' BorderThickness='1' VerticalAlignment='Center'>
              <TextBlock x:Name='Mark' Text='✓' Foreground='White' FontSize='13' FontWeight='Bold'
                         HorizontalAlignment='Center' VerticalAlignment='Center' Visibility='Collapsed'/>
            </Border>
            <ContentPresenter Margin='8,0,0,0' VerticalAlignment='Center'/>
          </StackPanel>
          <ControlTemplate.Triggers>
            <Trigger Property='IsChecked' Value='True'><Setter TargetName='Box' Property='Background' Value='{StaticResource AccentBrush}'/><Setter TargetName='Box' Property='BorderBrush' Value='{StaticResource AccentBrush}'/><Setter TargetName='Mark' Property='Visibility' Value='Visible'/></Trigger>
            <Trigger Property='IsMouseOver' Value='True'><Setter TargetName='Box' Property='BorderBrush' Value='{StaticResource FocusBrush}'/></Trigger>
            <Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='Box' Property='BorderBrush' Value='{StaticResource FocusBrush}'/><Setter TargetName='Box' Property='BorderThickness' Value='2'/></Trigger>
            <Trigger Property='IsEnabled' Value='False'><Setter Property='Opacity' Value='0.4'/></Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style TargetType='DataGrid'>
    <Setter Property='Background' Value='Transparent'/>
    <Setter Property='Foreground' Value='{StaticResource TextBrush}'/>
    <Setter Property='BorderThickness' Value='0'/>
    <Setter Property='GridLinesVisibility' Value='Horizontal'/>
    <Setter Property='HorizontalGridLinesBrush' Value='{StaticResource BorderBrush}'/>
    <Setter Property='RowBackground' Value='Transparent'/>
    <Setter Property='AlternatingRowBackground' Value='#0CFFFFFF'/>
    <Setter Property='HeadersVisibility' Value='Column'/>
    <Setter Property='RowHeight' Value='54'/>
    <Setter Property='ColumnHeaderHeight' Value='38'/>
    <Setter Property='AutoGenerateColumns' Value='False'/>
    <Setter Property='CanUserAddRows' Value='False'/>
    <Setter Property='CanUserDeleteRows' Value='False'/>
    <Setter Property='CanUserResizeRows' Value='False'/>
    <Setter Property='SelectionMode' Value='Single'/>
    <Setter Property='SelectionUnit' Value='FullRow'/>
  </Style>

  <Style TargetType='DataGridColumnHeader'>
    <Setter Property='Background' Value='{StaticResource SurfaceRaisedBrush}'/>
    <Setter Property='Foreground' Value='{StaticResource MutedTextBrush}'/>
    <Setter Property='BorderBrush' Value='{StaticResource BorderBrush}'/>
    <Setter Property='BorderThickness' Value='0,0,0,1'/>
    <Setter Property='Padding' Value='12,0'/>
    <Setter Property='FontWeight' Value='SemiBold'/>
    <Setter Property='FontSize' Value='12'/>
  </Style>

  <Style TargetType='DataGridRow'>
    <Setter Property='Foreground' Value='{StaticResource TextBrush}'/>
    <Setter Property='Background' Value='Transparent'/>
    <Setter Property='BorderThickness' Value='0'/>
    <Setter Property='FocusVisualStyle' Value='{x:Null}'/>
    <Style.Triggers>
      <Trigger Property='IsMouseOver' Value='True'><Setter Property='Background' Value='{StaticResource SurfaceHoverBrush}'/></Trigger>
      <Trigger Property='IsSelected' Value='True'><Setter Property='Background' Value='#253E5C'/><Setter Property='Foreground' Value='White'/></Trigger>
    </Style.Triggers>
  </Style>

  <Style TargetType='DataGridCell'>
    <Setter Property='BorderThickness' Value='0'/>
    <Setter Property='Padding' Value='12,0'/>
    <Setter Property='VerticalContentAlignment' Value='Center'/>
    <Setter Property='FocusVisualStyle' Value='{x:Null}'/>
  </Style>

  <Style TargetType='ListBox'>
    <Setter Property='Background' Value='Transparent'/>
    <Setter Property='BorderThickness' Value='0'/>
    <Setter Property='Foreground' Value='{StaticResource TextBrush}'/>
  </Style>

  <Style TargetType='ListBoxItem'>
    <Setter Property='Padding' Value='8'/>
    <Setter Property='Margin' Value='0,3'/>
    <Setter Property='HorizontalContentAlignment' Value='Stretch'/>
    <Setter Property='FocusVisualStyle' Value='{x:Null}'/>
    <Style.Triggers>
      <Trigger Property='IsMouseOver' Value='True'><Setter Property='Background' Value='{StaticResource SurfaceHoverBrush}'/></Trigger>
      <Trigger Property='IsSelected' Value='True'><Setter Property='Background' Value='#253E5C'/></Trigger>
    </Style.Triggers>
  </Style>

  <Style TargetType='ToolTip'>
    <Setter Property='Background' Value='#25344B'/>
    <Setter Property='Foreground' Value='White'/>
    <Setter Property='BorderBrush' Value='{StaticResource BorderBrush}'/>
    <Setter Property='BorderThickness' Value='1'/>
    <Setter Property='Padding' Value='10,7'/>
  </Style>
</ResourceDictionary>";

            ResourceDictionary dictionary;
            using (MemoryStream stream = new MemoryStream(Encoding.UTF8.GetBytes(xaml)))
            using (XmlReader reader = XmlReader.Create(stream))
                dictionary = (ResourceDictionary)XamlReader.Load(reader);

            if (SystemParameters.HighContrast)
            {
                dictionary["BackgroundBrush"] = SystemColors.WindowBrush;
                dictionary["SidebarBrush"] = SystemColors.ControlBrush;
                dictionary["SurfaceBrush"] = SystemColors.ControlBrush;
                dictionary["SurfaceRaisedBrush"] = SystemColors.ControlBrush;
                dictionary["SurfaceHoverBrush"] = SystemColors.HighlightBrush;
                dictionary["AccentBrush"] = SystemColors.HighlightBrush;
                dictionary["AccentHoverBrush"] = SystemColors.HighlightBrush;
                dictionary["AccentPressedBrush"] = SystemColors.HighlightBrush;
                dictionary["TextBrush"] = SystemColors.WindowTextBrush;
                dictionary["MutedTextBrush"] = SystemColors.WindowTextBrush;
                dictionary["SubtleTextBrush"] = SystemColors.GrayTextBrush;
                dictionary["BorderBrush"] = SystemColors.ActiveBorderBrush;
                dictionary["FocusBrush"] = SystemColors.HighlightBrush;
                dictionary["DangerBrush"] = SystemColors.WindowTextBrush;
            }
            return dictionary;
        }
    }
}
