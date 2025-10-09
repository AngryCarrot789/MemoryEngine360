// 
// Copyright (c) 2024-2025 REghZy
// 
// This file is part of MemoryEngine360.
// 
// MemoryEngine360 is free software; you can redistribute it and/or
// modify it under the terms of the GNU General Public License
// as published by the Free Software Foundation; either
// version 3.0 of the License, or (at your option) any later version.
// 
// MemoryEngine360 is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU
// Lesser General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with MemoryEngine360. If not, see <https://www.gnu.org/licenses/>.
// 

using PFXToolKitUI;
using PFXToolKitUI.Icons;
using PFXToolKitUI.Themes;
using PFXToolKitUI.Themes.Gradients;
using SkiaSharp;

namespace MemEngine360;

public static class SimpleIcons {
    public static readonly Icon MemoryIcon =
        IconManager.Instance.RegisterGeometryIcon(
            nameof(MemoryIcon),
            [
                new GeometryEntry("M0 2 12 2 12 10 0 10Z", BrushManager.Instance.CreateConstant(SKColors.MediumSeaGreen)),
                new GeometryEntry("M1 0 3 0 3 2 1 2ZM9 0 11 0 11 2 9 2ZM1 10 3 10 3 12 1 12ZM9 10 11 10 11 12 9 12ZM5 2 5 0 7 0 7 2 5 2M5 10 5 12 7 12 7 10 5 10", BrushManager.Instance.CreateConstant(SKColors.Gold))
            ]);

    public static readonly Icon DownloadMemoryIcon =
        IconManager.Instance.RegisterGeometryIcon(
            nameof(DownloadMemoryIcon),
            [
                new GeometryEntry("M0 2 12 2 12 10 0 10Z", BrushManager.Instance.CreateConstant(SKColors.MediumSeaGreen)),
                new GeometryEntry("M1 0 3 0 3 2 1 2ZM9 0 11 0 11 2 9 2ZM1 10 3 10 3 12 1 12ZM9 10 11 10 11 12 9 12ZM5 2 5 0 7 0 7 2 5 2M5 10 5 12 7 12 7 10 5 10", BrushManager.Instance.CreateConstant(SKColors.Gold)),
                new GeometryEntry("M9.5 16 5 12V10H8V3h3v7h3v2l-4.5 4Z", StandardIcons.GlyphBrush)
            ]);

    public static readonly Icon MemoryIconTable =
        IconManager.Instance.RegisterGeometryIcon(
            nameof(MemoryIconTable),
            [
                new GeometryEntry("M10.5883 0C11.3669 0 12 .6332 12 1.4117v9.1765c0 .7786-.6332 1.4117-1.4117 1.4117H1.4117C.6332 12 0 11.3669 0 10.5883V1.4117C0 .6332.6332 0 1 0h9.1765Zm-2.1177 10.5883h2.119v-2.1177h-2.119v2.1177Zm-3.5295-3.5295h2.1177V4.9412H4.9412v2.1177Zm0 3.5295h2.1177v-2.1177H4.9412v2.1177Zm-3.5295-3.5295h2.1177V4.9412H1.4117v2.1177Zm0 3.5295h2.1177v-2.1177H1.4117v2.1177Zm7.0589-3.5295h2.1184V4.9412h-2.1184v2.1177Z",
                    BrushManager.Instance.CreateConstant(SKColors.MediumSeaGreen))
            ]);

    private const string ControllerSvg = "m20.941 16.4-1.5-5c.1-.5.2-.9.2-1.4 0-2.8-2.2-5-5-5-1.1 0-2.1.4-3 1h-2.2c-.9-.6-1.9-1-3-1-2.8 0-5 2.2-5 5 0 .5.1.9.2 1.4l-1.5 5c-.3.8-.1 1.7.4 2.4.6.8 1.4 1.2 2.3 1.2.9 0 1.7-.4 2.2-1.1l3.2-3.9h4.6l3.1 3.9c.5.7 1.4 1.1 2.2 1.1.9 0 1.7-.4 2.3-1.1.6-.8.7-1.7.5-2.5zm-6.2-4.7c-.2.2-.4.3-.7.3-.3 0-.5-.1-.7-.3-.2-.2-.3-.4-.3-.7s.1-.5.3-.7c.1-.1.2-.2.3-.2.4-.2.8-.1 1.1.2 0 0 .1.1.1.1 0 .1.1.1.1.2s0 .1.1.2c0 .1 0 .1 0 .2 0 .3-.1.5-.3.7zm1-2c-.2.2-.4.3-.7.3-.1 0-.3 0-.4-.1s-.2-.1-.3-.2c-.2-.2-.3-.4-.3-.7s.1-.5.3-.7c.1-.1.2-.2.3-.2.4-.1.8-.1 1.1.2.2.2.3.4.3.7 0 .3-.1.5-.3.7z";
    private const string UploadArrowSvg = "M24 0H15.5L14 1.5 16.8358 4.3358 10.5858 10.5858 13.4142 13.4142 19.6642 7.1642 22.5 10 24 8.5 24 0Z";

    public static readonly Icon ConnectToConsoleIcon =
        IconManager.Instance.RegisterGeometryIcon(
            nameof(ConnectToConsoleIcon),
            [
                new GeometryEntry(ControllerSvg, BrushManager.Instance.CreateConstant(SKColors.MediumSeaGreen)),
                new GeometryEntry(UploadArrowSvg, StandardIcons.GlyphBrush),
            ]);

    public static readonly Icon ConnectToConsoleDedicatedIcon =
        IconManager.Instance.RegisterGeometryIcon(
            nameof(ConnectToConsoleDedicatedIcon),
            [
                new GeometryEntry(ControllerSvg, BrushManager.Instance.CreateConstant(SKColors.MediumSeaGreen)),
                new GeometryEntry(UploadArrowSvg, BrushManager.Instance.CreateConstant(SKColors.Yellow)),
            ]);

    public static readonly Icon DeleteAllRowsIcon =
        IconManager.Instance.RegisterGeometryIcon(
            nameof(DeleteAllRowsIcon),
            [
                new GeometryEntry("M7.019 9.188l.7.7 1.6-1.6 1.6 1.6.8-.7L10.018 7.59l1.7-1.6-.8-.8-1.6 1.7-1.6-1.7-.7.8 1.6 1.6-1.6 1.6zM0 1h12V0H0v1zm0 3h12V3H0v1zm6 3V6H0v1h6zM6 10v-1H0v1h6z", StandardIcons.GlyphBrush),
            ]);

    public static readonly Icon DeleteRowIcon =
        IconManager.Instance.RegisterGeometryIcon(
            nameof(DeleteRowIcon),
            [
                new GeometryEntry("M.3789 6.1563C.1689 6.1563 0 6.3895 0 6.6797v4.7949C0 11.7647.1689 12 .3789 12H13.6211c.2101 0 .3789-.2353.3789-.5254V6.6797c0-.2902-.1688-.5234-.3789-.5234H.3789zm.6797 1.2109h3.2148v3.4219H1.0586V7.3672zm4.2832 0h3.2148v3.4219H5.3418V7.3672zm4.2832 0h3.2168v3.4219H9.625V7.3672z", StandardIcons.GlyphBrush),
                new GeometryEntry("M10.6999 11.5999q-.2558 0-.4348-.1791l-.8698-.87q-.1791-.1791-.1791-.435 0-.2559.1791-.435l1.8802-1.8808-1.8802-1.8808q-.1791-.1791-.1791-.435 0-.2559.1791-.435l.8698-.87Q10.4441 4 10.6999 4t.4349.1791l1.8802 1.8808L14.8952 4.1791Q15.0742 4 15.33 4t.4349.1791l.8698.87q.179.1791.179.435 0 .2559-.179.435l-1.8802 1.8808 1.8802 1.8808q.179.1791.179.435 0 .2559-.179.435l-.8698.87q-.1791.1791-.4349.1791t-.4348-.1791L13.015 9.54 11.1348 11.4208q-.1791.1791-.4349.1791z", BrushManager.Instance.CreateConstant(SKColors.Red)),
            ]);

    public static readonly Icon AddRowIcon =
        IconManager.Instance.RegisterGeometryIcon(
            nameof(AddRowIcon),
            [
                new GeometryEntry("M.3789 6.1563C.1689 6.1563 0 6.3895 0 6.6797v4.7949C0 11.7647.1689 12 .3789 12H13.6211c.2101 0 .3789-.2353.3789-.5254V6.6797c0-.2902-.1688-.5234-.3789-.5234H.3789zm.6797 1.2109h3.2148v3.4219H1.0586V7.3672zm4.2832 0h3.2148v3.4219H5.3418V7.3672zm4.2832 0h3.2168v3.4219H9.625V7.3672z", StandardIcons.GlyphBrush),
                new GeometryEntry("M8.3281 9.1262q-.1809-.1809-.1808-.4341l.0001-1.2302q-0-.2533.1809-.4342.1809-.1809.4342-.1809l2.6594-.0004.0004-2.6594q-0-.2533.1809-.4342.1809-.1809.4342-.1809l1.2302-.0001Q13.5211 3.5714 13.702 3.7523t.1809.4342l-.0004 2.6594L16.5419 6.8454Q16.7951 6.8454 16.976 7.0262t.1809.4342l-.0001 1.2302q-.0001.2532-.181.4342-.1809.1809-.4342.181l-2.6594.0004-.0004 2.6594q-.0001.2532-.181.4342-.1809.1809-.4342.181l-1.2302.0001q-.2533 0-.4342-.1809t-.1808-.4341L11.4217 9.3067 8.7622 9.3071q-.2533 0-.4342-.1809z", BrushManager.Instance.CreateConstant(SKColors.LawnGreen)),
            ]);

    public static readonly Icon RedArrowCopyToSavedResultsIcon =
        IconManager.Instance.RegisterGeometryIcon(
            nameof(RedArrowCopyToSavedResultsIcon),
            [
                new GeometryEntry("M13.5 13.45V4.95L12 3.45 9.1642 6.2858 2.9142.0358.0858 2.8642 6.3358 9.1142 3.5 11.95 5 13.45 13.5 13.45Z", BrushManager.Instance.CreateConstant(SKColors.Brown)),
            ]);

    // https://www.svgrepo.com/svg/452137/xbox
    public static readonly Icon Xbox360Icon =
        IconManager.Instance.RegisterGeometryIcon(
            nameof(Xbox360Icon),
            [
                new GeometryEntry("M16 0A1 1 0 0016 32 1 1 0 0016 0",
                    BrushManager.Instance.CreateConstantRadialGradient([new GradientStop(SKColor.Parse("#FAFAFA")), new GradientStop(SKColor.Parse("#EFEFEF"), 0.499976), new GradientStop(SKColor.Parse("#C0BEC0"), 0.828794), new GradientStop(SKColor.Parse("#879288"), 1.0)], radius: 1.0)),
                new GeometryEntry("M6.75137 26.5333C6.73227 26.5193 6.7127 26.5016 6.69312 26.4801C6.33073 26.1504 5.74045 25.6304 5.12362 24.8444C4.5909 23.6889 4.89287 21.748 6.20386 19.0075C8.05357 15.1409 11.0131 11.2151 12.2265 9.8225C11.8813 9.43732 10.8119 8.38253 9.29659 7.24477C7.7813 6.10701 6.67738 5.99047 6.29264 6.05961C6.64779 5.67443 7.47646 4.94852 8.17196 4.45964C10.3265 3.39299 14.3081 5.19543 16.0148 6.22257V13.2891C11.6642 16.6372 7.63924 21.8075 6.82536 23.8222C6.20418 25.36 6.44975 26.2128 6.69312 26.4801C6.71325 26.4984 6.73269 26.5161 6.75137 26.5333Z",
                    BrushManager.Instance.CreateConstantLinearGradient([new GradientStop(SKColor.Parse("#008B00")), new GradientStop(SKColor.Parse("#6CC329"), 1.0)], startPoint: new RelativePoint(13F, 4.725F, RelativeUnit.Absolute), endPoint: new RelativePoint(13.23F, 12.785F, RelativeUnit.Absolute))),
                new GeometryEntry("M6.75137 26.5331C6.73227 26.5191 6.7127 26.5014 6.69311 26.4799C6.33072 26.1502 5.74045 25.6302 5.12362 24.8442C4.5909 23.6887 4.89287 21.7477 6.20386 19.0073C8.35133 14.5183 12.2265 9.82227 12.2265 9.82227C12.2265 9.82227 12.9185 11.5405 14.4888 14.5183C10.4444 18.1034 7.63923 21.8072 6.82536 23.822C6.20418 25.3598 6.44974 26.2126 6.69311 26.4799C6.71325 26.4982 6.73268 26.5159 6.75137 26.5331Z",
                    BrushManager.Instance.CreateConstantLinearGradient([new GradientStop(SKColor.Parse("#008C00")), new GradientStop(SKColor.Parse("#48BF21"), 1.0)], startPoint: new RelativePoint(10.1481F, 12.6667F, RelativeUnit.Absolute), endPoint: new RelativePoint(12.874F, 16.4F, RelativeUnit.Absolute))),
                new GeometryEntry("M25.2779 26.5333C25.297 26.5193 25.3166 26.5016 25.3362 26.4801C25.6986 26.1504 26.2888 25.6304 26.9057 24.8444C27.4384 23.6889 27.1364 21.748 25.8254 19.0075C23.9757 15.1409 21.0162 11.2151 19.8028 9.8225C20.148 9.43732 21.2174 8.38253 22.7327 7.24477C24.248 6.10701 25.3519 5.99047 25.7367 6.05961C25.3815 5.67443 24.5528 4.94852 23.8573 4.45964C21.7028 3.39299 17.7212 5.19543 16.0145 6.22257V13.2891C20.3651 16.6372 24.3901 21.8075 25.2039 23.8222C25.8251 25.36 25.5796 26.2128 25.3362 26.4801C25.316 26.4984 25.2966 26.5161 25.2779 26.5333Z",
                    BrushManager.Instance.CreateConstantLinearGradient([new GradientStop(SKColor.Parse("#008B00")), new GradientStop(SKColor.Parse("#6CC329"), 1.0)], startPoint: new RelativePoint(19.0368F, 4.72589F, RelativeUnit.Absolute), endPoint: new RelativePoint(18.7997F, 12.7852F, RelativeUnit.Absolute))),
                new GeometryEntry("M25.2779 26.5331C25.297 26.5191 25.3166 26.5014 25.3362 26.4799C25.6986 26.1502 26.2888 25.6302 26.9057 24.8442C27.4384 23.6887 27.1364 21.7477 25.8254 19.0073C23.678 14.5183 19.8028 9.82227 19.8028 9.82227C19.8028 9.82227 19.1108 11.5405 17.5405 14.5183C21.5849 18.1034 24.3901 21.8072 25.2039 23.822C25.8251 25.3598 25.5796 26.2126 25.3362 26.4799C25.3161 26.4982 25.2966 26.5159 25.2779 26.5331Z",
                    BrushManager.Instance.CreateConstantLinearGradient([new GradientStop(SKColor.Parse("#008C00")), new GradientStop(SKColor.Parse("#48BF21"), 1.0)], startPoint: new RelativePoint(21.8812F, 12.6667F, RelativeUnit.Absolute), endPoint: new RelativePoint(19.1553F, 16.4F, RelativeUnit.Absolute))),
            ]);

    public static readonly Icon CursedXbox360Icon =
        IconManager.Instance.RegisterGeometryIcon(
            nameof(CursedXbox360Icon),
            [
                new GeometryEntry("M16 0A1 1 0 0016 32 1 1 0 0016 0",
                    BrushManager.Instance.CreateConstant(SKColors.DarkRed)),
                new GeometryEntry("M6.75137 26.5333C6.73227 26.5193 6.7127 26.5016 6.69312 26.4801C6.33073 26.1504 5.74045 25.6304 5.12362 24.8444C4.5909 23.6889 4.89287 21.748 6.20386 19.0075C8.05357 15.1409 11.0131 11.2151 12.2265 9.8225C11.8813 9.43732 10.8119 8.38253 9.29659 7.24477C7.7813 6.10701 6.67738 5.99047 6.29264 6.05961C6.64779 5.67443 7.47646 4.94852 8.17196 4.45964C10.3265 3.39299 14.3081 5.19543 16.0148 6.22257V13.2891C11.6642 16.6372 7.63924 21.8075 6.82536 23.8222C6.20418 25.36 6.44975 26.2128 6.69312 26.4801C6.71325 26.4984 6.73269 26.5161 6.75137 26.5333Z",
                    BrushManager.Instance.CreateConstantLinearGradient([new GradientStop(SKColor.Parse("#008B00")), new GradientStop(SKColor.Parse("#6CC329"), 1.0)], startPoint: new RelativePoint(13F, 4.725F, RelativeUnit.Absolute), endPoint: new RelativePoint(13.23F, 12.785F, RelativeUnit.Absolute))),
                new GeometryEntry("M6.75137 26.5331C6.73227 26.5191 6.7127 26.5014 6.69311 26.4799C6.33072 26.1502 5.74045 25.6302 5.12362 24.8442C4.5909 23.6887 4.89287 21.7477 6.20386 19.0073C8.35133 14.5183 12.2265 9.82227 12.2265 9.82227C12.2265 9.82227 12.9185 11.5405 14.4888 14.5183C10.4444 18.1034 7.63923 21.8072 6.82536 23.822C6.20418 25.3598 6.44974 26.2126 6.69311 26.4799C6.71325 26.4982 6.73268 26.5159 6.75137 26.5331Z",
                    BrushManager.Instance.CreateConstantLinearGradient([new GradientStop(SKColor.Parse("#008C00")), new GradientStop(SKColor.Parse("#48BF21"), 1.0)], startPoint: new RelativePoint(10.1481F, 12.6667F, RelativeUnit.Absolute), endPoint: new RelativePoint(12.874F, 16.4F, RelativeUnit.Absolute))),
                new GeometryEntry("M25.2779 26.5333C25.297 26.5193 25.3166 26.5016 25.3362 26.4801C25.6986 26.1504 26.2888 25.6304 26.9057 24.8444C27.4384 23.6889 27.1364 21.748 25.8254 19.0075C23.9757 15.1409 21.0162 11.2151 19.8028 9.8225C20.148 9.43732 21.2174 8.38253 22.7327 7.24477C24.248 6.10701 25.3519 5.99047 25.7367 6.05961C25.3815 5.67443 24.5528 4.94852 23.8573 4.45964C21.7028 3.39299 17.7212 5.19543 16.0145 6.22257V13.2891C20.3651 16.6372 24.3901 21.8075 25.2039 23.8222C25.8251 25.36 25.5796 26.2128 25.3362 26.4801C25.316 26.4984 25.2966 26.5161 25.2779 26.5333Z",
                    BrushManager.Instance.CreateConstantLinearGradient([new GradientStop(SKColor.Parse("#008B00")), new GradientStop(SKColor.Parse("#6CC329"), 1.0)], startPoint: new RelativePoint(19.0368F, 4.72589F, RelativeUnit.Absolute), endPoint: new RelativePoint(18.7997F, 12.7852F, RelativeUnit.Absolute))),
                new GeometryEntry("M25.2779 26.5331C25.297 26.5191 25.3166 26.5014 25.3362 26.4799C25.6986 26.1502 26.2888 25.6302 26.9057 24.8442C27.4384 23.6887 27.1364 21.7477 25.8254 19.0073C23.678 14.5183 19.8028 9.82227 19.8028 9.82227C19.8028 9.82227 19.1108 11.5405 17.5405 14.5183C21.5849 18.1034 24.3901 21.8072 25.2039 23.822C25.8251 25.3598 25.5796 26.2126 25.3362 26.4799C25.3161 26.4982 25.2966 26.5159 25.2779 26.5331Z",
                    BrushManager.Instance.CreateConstantLinearGradient([new GradientStop(SKColor.Parse("#008C00")), new GradientStop(SKColor.Parse("#48BF21"), 1.0)], startPoint: new RelativePoint(21.8812F, 12.6667F, RelativeUnit.Absolute), endPoint: new RelativePoint(19.1553F, 16.4F, RelativeUnit.Absolute))),
            ]);

    public static readonly Icon CopyHexSelectionToRefreshRangeIcon =
        IconManager.Instance.RegisterGeometryIcon(
            nameof(CopyHexSelectionToRefreshRangeIcon),
            [
                new GeometryEntry("M.5.55.5 9.05 2 10.55 4.8358 7.7142 11.0858 13.9642 13.9142 11.1358 7.6642 4.8858 10.5 2.05 9 .55.5.55Z", StandardIcons.GlyphBrush),
            ]);

    // https://www.svgrepo.com/svg/332278/clear
    public static readonly Icon ClearHexRefreshRangeIcon =
        IconManager.Instance.RegisterGeometryIcon(
            nameof(ClearHexRefreshRangeIcon),
            [
                new GeometryEntry("M42.5056 42.0889l-2.9444-16.9778H40.5556c.8 0 1.4444-.6444 1.4444-1.4444V13c0-.8-.6444-1.4444-1.4444-1.4444H26.8889V1.4444c0-.8-.6444-1.4444-1.4444-1.4444H16.5556c-.8 0-1.4444.6444-1.4444 1.4444v10.1111H1.4444c-.8 0-1.4444.6444-1.4444 1.4444v10.6667c0 .8.6444 1.4444 1.4444 1.4444h.9944l-2.9444 16.9778c-.0167.0833-.0222.1667-.0222.2444 0 .8.6444 1.4444 1.4444 1.4444h40.1667c.0833 0 .1667-.0056.2444-.0222.7889-.1333 1.3167-.8833 1.1778-1.6667zM3.8889 15.4444h15.1111V3.8889h4v11.5556h15.1111v5.7778H3.8889V15.4444zm26 24.4444V31.2222c0-.2444-.2-.4444-.4444-.4444h-2.6667c-.2444 0-.4444.2-.4444.4444v8.6667H15.6667V31.2222c0-.2444-.2-.4444-.4444-.4444h-2.6667c-.2444 0-.4444.2-.4444.4444v8.6667H3.8222l2.5056-14.4444H35.6667l2.5056 14.4444H29.8889z", StandardIcons.GlyphBrush),
            ]);


    // Modified version of https://www.svgrepo.com/svg/486453/open-file-filled
    public static readonly Icon OpenFileIcon =
        IconManager.Instance.RegisterGeometryIcon(
            nameof(OpenFileIcon),
            [
                // new GeometryEntry("M58.9173 33.8232l-10.4756 19.826c-.5125.9858-1.9653 2.0285-3.0872 2.0285l-42.0096.0077c-.8875 0-1.7384-.3518-2.3655-.9794C.3523 54.0783 0 53.2283 0 52.3405l.0068-32.6209c0-1.8448 1.4955-3.3417 3.3405-3.3435l3.0715-.0029v2.8496H4.3639c-.4022 0-.7885.1596-1.0731.4442-.2843.2846-.4442.6706-.4442 1.0731l.003 30.5796c0 .8388.6797 1.5173 1.5176 1.5173h2.1045l9.928-20.0836c.5609-1.1219 1.6208-2.0286 2.7411-2.0286h24.314l.0083-8.0823c1.5876.1472 2.8406 1.4641 2.8406 3.0893v4.9931H57.455C58.7719 30.7384 59.8808 32.0406 58.9173 33.8232zM8.3558 42.7272c-.0907-9.9969 0-39.9884 0-39.9884 0-1.5132 1.2306-2.7429 2.7423-2.7429h21.9614c.3518 0 .6874.1472.9251.4061l7.1 7.7141c.2128.2323.3321.5364.3321.8515v19.9839h-2.3034V10.6932c0-.3159-.2565-.5725-.5728-.5725h-5.4343c-.636 0-1.1532-.5163-1.1532-1.1511V2.8721c0-.316-.2565-.5725-.5728-.5725H11.0995c-.2423 0-.4395.1971-.4395.4392v37.4739l-1.7626 3.566C8.8975 43.7781 8.3649 43.7391 8.3558 42.7272zM34.2588 7.8182H37.68l-3.4212-3.7188V7.8182zM35.5065 12.4667H14.2813c-.782 0-1.4168.6354-1.4168 1.4174 0 .7814.6354 1.4171 1.4168 1.4171h21.2258c.782 0 1.4187-.6362 1.4187-1.4171C36.9245 13.1027 36.288 12.4667 35.5065 12.4667zM36.9245 22.1376c0-.7814-.6359-1.4162-1.418-1.4162H14.2813c-.782 0-1.4168.636-1.4168 1.4162 0 .7814.6354 1.4162 1.4168 1.4162h21.2258C36.288 23.5539 36.9245 22.919 36.9245 22.1376zM12.8636 30.447c0 .7813.6357 1.4162 1.4171 1.4162h.5089c.8958-2.4967 3.1164-2.8324 3.1164-2.8324h-3.6253C13.5005 29.0308 12.8636 29.6656 12.8636 30.447z", StandardIcons.GlyphBrush),
                new GeometryEntry("M7 0 7 2 16 2 16 4 3 4 .2 12.5 0 12.5 0 0 7 0ZM3.8679 5.476 16 5.4761 13.8621 12.5 1.6873 12.5 3.8679 5.476Z", StandardIcons.GlyphBrush),
            ]);

    // https://www.svgrepo.com/svg/273782/save-file
    // public static readonly Icon SaveFileIcon =
    //     IconManager.Instance.RegisterGeometryIcon(
    //         nameof(SaveFileIcon),
    //         [
    //             new GeometryEntry("M424.229 12.854 424.229 204.8 87.771 204.8 87.771 0 0 0 0 512 87.771 512 87.771 307.2 424.229 307.2 424.229 512 512 512 512 100.626z", StandardIcons.GlyphBrush),
    //             new GeometryEntry("M131.657,351.086V512h248.686V351.086H131.657z M329.143,446.171H182.857v-43.886h146.286V446.171z", StandardIcons.GlyphBrush),
    //             new GeometryEntry("M131.657,0v160.914h248.686V0H131.657z M204.8,124.343h-43.886V51.2H204.8V124.343z", StandardIcons.GlyphBrush),
    //         ]);
    public static readonly Icon SaveFileIcon =
        IconManager.Instance.RegisterGeometryIcon(
            nameof(SaveFileIcon),
            [
                new GeometryEntry("M10 0 10 5 2 5 2 0 0 0 0 12 2 12 2 7 10 7 10 12 12 12 12 3z", StandardIcons.GlyphBrush),
                new GeometryEntry("M3 8V12H9V8H3ZM8 11H4V9H8V11Z", StandardIcons.GlyphBrush),
                new GeometryEntry("M3 0v4h6V0H3zM5 3h-1V1H5V3z", StandardIcons.GlyphBrush),
            ]);

    // https://www.svgrepo.com/svg/368622/csv
    // https://www.svgrepo.com/svg/374198/xml
    // CSV: M4.5.5V0H4v.5h.5Zm0 2H4V3h.5V2.5Zm2 0H7V2H6.5v.5Zm0 2V5H7v-.5H6.5Zm2-1H8v.2071l.1464.1464L8.5 3.5Zm1 1-.3536.3536.3536.3535.3536-.3535L9.5 4.5Zm1-1 .3536.3536L11 3.7071V3.5h-.5ZM.5.5V0H0v.5h.5Zm0 4H0V5h.5ZH0Zh10ZM7 0H4.5V1H7V0ZM4 .5v2H5v-2H4ZM4.5 3h2V2h-2V3ZM6 2.5v2H7v-2H6ZM6.5 4H4v1H6.5V4ZM8 0V3.5h1V0H8Zm.1464 3.8536 1 1 .7072-.7072-1-1-.7072.7071Zm1.7072 1 1-1-.7072-.7071-1 1 .7072.7072ZM11 3.5V0H10V3.5h1ZM3 0H.5V1H3V0ZM0 .5v4H1v-4H0ZM.5 5H3V4H.5v1Z
    // XML: M12.89,3l2,.4L11.11,21l-2-.4L12.89,3m6.7,9L16,8.41V5.58L22.42,12,16,18.41V15.58L19.59,12m-18,0L8,5.58V8.41L4.41,12,8,15.58v2.83Z
    // DIR: M11.7407 3.0144C11.397 2.4087 10.7462 2 10 2L8 2 6.2929.2929C6.1054.1054 5.851 0 5.5858 0L2 0C.8954 0 0 .8954 0 2L0 10C0 11.1046.8954 12 2 12L11 12C11.0024 12 11.0047 12 11.0071 12L11.6516 12C12.5554 12 13.3469 11.3939 13.5825 10.5213L14.9286 5.5357C15.2718 4.2646 14.3144 3.0144 12.9977 3.0144L11.7407 3.0144ZM3.3483 5.0144 2.0022 10 11.6516 10 12.9977 5.0144 3.3483 5.0144Z

    public static readonly Icon OpenFileCSVIcon =
        IconManager.Instance.RegisterGeometryIcon(
            nameof(OpenFileCSVIcon),
            [
                new GeometryEntry("M8.5 4.5V4H8v.5h.5Zm0 2H8V7h.5V6.5Zm2 0H11V6H10.5v.5Zm0 2V9H11v-.5H10.5Zm2-1H12v.2071l.1464.1464L12.5 7.5Zm1 1-.3536.3536.3536.3535.3536-.3535L13.5 8.5Zm1-1 .3536.3536L15 7.7071V7.5h-.5ZM4.5 4.5V4H4v.5h.5Zm0 4H4V9h.5ZH4Zh10ZM11 4H8.5V5H11V4ZM8 4.5v2H9v-2H8ZM8.5 7h2V6h-2V7ZM10 6.5v2H11v-2H10ZM10.5 8H8v1H10.5V8ZM12 4V7.5h1V4H12Zm.1464 3.8536 1 1 .7072-.7072-1-1-.7072.7071Zm1.7072 1 1-1-.7072-.7071-1 1 .7072.7072ZM15 7.5V4H14V7.5h1ZM7 4H4.5V5H7V4ZM4 4.5v4H5v-4H4ZM4.5 9H7V8H4.5v1Z", BrushManager.Instance.CreateConstant(SKColors.LawnGreen)),
            ]);

    public static readonly Icon OpenFileXMLIcon =
        IconManager.Instance.RegisterGeometryIcon(
            nameof(OpenFileXMLIcon),
            [
                new GeometryEntry("M9.6675 2.25l1.5.3L8.3325 15.75l-1.5-.3L9.6675 2.25m5.025 6.75L12 6.3075V4.185L16.815 9 12 13.8075V11.685L14.6925 9m-13.5 0L6 4.185V6.3075L3.3075 9 6 11.685v2.1225Z", BrushManager.Instance.CreateConstant(SKColors.OrangeRed))
            ]);
}