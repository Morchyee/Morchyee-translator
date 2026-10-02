import {createTheme} from "@mui/material/styles";
import {Locale, uiFont} from "../../i18n/localization";

export const desktopFont = '"Segoe UI Variable", "Segoe UI", system-ui, sans-serif';
export function desktopTheme(light: boolean, locale: Locale = "en") {
    return createTheme({
        palette: {mode: light ? "light" : "dark", primary: {main: light ? "#235fc4" : "#8ab4ff"},
            background: {default: light ? "#f7f8fa" : "#171b22", paper: light ? "#ffffff" : "#202630"},
            text: {primary: light ? "#202631" : "#f1f4f9", secondary: light ? "#586373" : "#aebacc"},
            divider: light ? "#dde2e9" : "#343e4d"},
        typography: {fontFamily: uiFont(locale), fontSize: 13,
            h1: {fontSize: 22, fontWeight: 600, lineHeight: 1.35},
            h2: {fontSize: 16, fontWeight: 600, lineHeight: 1.5},
            body1: {fontSize: 13, lineHeight: 1.6}, body2: {fontSize: 12, lineHeight: 1.6},
            button: {textTransform: "none", fontWeight: 600, fontSize: 13}},
        shape: {borderRadius: 6},
        components: {
            MuiButtonBase: {defaultProps: {disableRipple: true}},
            MuiButton: {defaultProps: {disableElevation: true, size: "small"}, styleOverrides: {root: {minHeight: 34, paddingInline: 14}}},
            MuiTextField: {defaultProps: {size: "small"}},
            MuiSelect: {defaultProps: {size: "small"}},
            MuiSwitch: {defaultProps: {size: "small"}, styleOverrides: {switchBase: {"&.Mui-focusVisible .MuiSwitch-thumb": {outline: `2px solid ${light ? "#235fc4" : "#8ab4ff"}`, outlineOffset: 3}}}},
            MuiAlert: {styleOverrides: {root: {fontSize: 13, alignItems: "center"}}},
            MuiOutlinedInput: {styleOverrides: {root: {fontSize: 13}, notchedOutline: {borderColor: light ? "#7d899a" : "#637187"}}},
            MuiMenuItem: {styleOverrides: {root: {fontSize: 13, minHeight: 34}}},
            MuiFormHelperText: {styleOverrides: {root: {marginLeft: 0}}}
        }
    });
}
