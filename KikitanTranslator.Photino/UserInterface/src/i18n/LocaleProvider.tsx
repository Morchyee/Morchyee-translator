import {createContext, ReactNode, useContext, useEffect, useMemo} from "react";
import {Locale, resolveLocale, Translate, translate} from "./localization";

const Context = createContext<{locale: Locale; t: Translate}>({locale: "en", t: (key, values) => translate("en", key, values)});
export function LocaleProvider({locale: preference, children}: {locale?: string; children: ReactNode}) {
    const locale = resolveLocale(preference, navigator.language);
    const value = useMemo(() => ({locale, t: ((key, values) => translate(locale, key, values)) as Translate}), [locale]);
    useEffect(() => { document.documentElement.lang = locale; }, [locale]);
    return <Context.Provider value={value}>{children}</Context.Provider>;
}
export const useLocale = () => useContext(Context);
