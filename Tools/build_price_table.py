#!/usr/bin/env python3
"""Generates Assets/Scripts/Core/Economy/RegionalPriceTable.cs: every country's price multiplier, currency and
exchange rate, so the game can show each country its own regional price in its own money.

    Tools/.external/pyenv/bin/python Tools/build_price_table.py <countries.json> <gdppc.json> <fx.json>

Inputs (fetched by hand, see README "Regional pricing"):
  countries.json : https://api.worldbank.org/v2/country?format=json&per_page=400                      (CC BY 4.0)
  gdppc.json     : https://api.worldbank.org/v2/country/all/indicator/NY.GDP.PCAP.CD?format=json&mrnev=1&per_page=1000
  fx.json        : https://open.er-api.com/v6/latest/USD   (ExchangeRate-API's free daily rates, with attribution)
Plus, from the venv: pycountry (the ISO 3166-1 list) and babel (CLDR: each country's currency and its symbol).

multiplier = clamp((GDP per capita / US GDP per capita) ^ ELASTICITY, FLOOR, 1.0)
An elasticity around 0.35 tracks how consumer price levels actually scale with income (Balassa-Samuelson), so
poorer countries get a real discount without prices collapsing.
"""
import datetime
import json
import os
import sys

import pycountry
from babel import Locale, UnknownLocaleError
from babel.numbers import get_currency_precision, get_currency_symbol, get_territory_currencies

ELASTICITY, FLOOR = 0.35, 0.20
OUT = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))),
                   "Assets", "Scripts", "Core", "Economy", "RegionalPriceTable.cs")

# Territories the World Bank doesn't list. Most share their parent country's economy (and so its price); the rest use
# published estimates of GDP per capita in US$ (IMF / national statistics offices / CIA World Factbook, recent years).
INHERIT = {"AX": "FI", "BL": "FR", "BV": "NO", "CC": "AU", "CX": "AU", "EH": "MA", "GS": "GB", "HM": "AU", "IO": "GB",
           "NF": "AU", "PN": "NZ", "SJ": "NO", "TF": "FR", "UM": "US", "VA": "IT", "GG": "JG", "JE": "JG"}
ESTIMATES = {"TW": 34400, "AI": 20000, "BQ": 28000, "CK": 22000, "FK": 70000, "GF": 18000, "GI": 90000, "GP": 27000,
             "KP": 1300, "MQ": 28000, "MS": 13000, "NU": 15000, "PM": 35000, "RE": 25000, "SH": 12000, "TK": 7000,
             "VG": 50000, "WF": 12000, "YT": 12500}
# No population: the standard (base) price.
BASE_PRICE = {"AQ"}
# Recent changes CLDR doesn't have yet.
CURRENCY_OVERRIDES = {"BG": "EUR"}   # Bulgaria adopted the euro on 1 January 2026
# Big-unit currencies priced in round numbers (¥480, ₩6,500, Rp 79.000) rather than ending in 9 (₹449, 299 ₽).
ROUND_STYLE = {"JPY", "KRW", "TWD", "CLP", "ISK", "HUF", "IDR", "VND", "COP", "PYG", "UGX", "TZS", "KHR", "LAK", "MMK",
               "IRR", "IQD", "LBP", "SYP", "MNT", "UZS", "GNF", "MGA", "KPW", "RWF", "BIF", "XAF", "XOF", "KMF", "DJF",
               "VUV", "XPF", "CDF", "SOS", "SLE", "SSP", "STN", "MWK", "LRD", "SDG", "YER", "BYR", "ZMW", "AMD", "KZT"}
# Currencies with cents that are still priced in whole units (₹29, 49 ₽, 29 kr, ¥18): no cents even under 100.
WHOLE_STYLE = {"INR", "RUB", "UAH", "PHP", "THB", "CZK", "TWD", "EGP", "PKR", "LKR", "NPR", "BDT", "KES", "CNY", "HKD",
               "MXN", "UYU", "DOP", "NOK", "SEK", "DKK", "ARS", "JMD", "NGN", "GHS", "MUR", "MOP", "TRY"}
# Characters a currency symbol may use and still be drawn by every font the game prices in (the font builder adds the
# currency signs); anything else (Arabic or Cyrillic abbreviations) is shown as the ISO code instead.
SAFE_SYMBOL = set("$£€¥¢₹₩₺₽₴₪₫₱฿₦₵₸₼₾₲₡₭₮₨₳₤₥₧₯₰₠₣₢ ") | set("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz.") \
    | set("łčšžÁáéíóúŁČŠŽ")


def likely_locale(cc):
    try:
        return Locale.parse(f"und_{cc}")
    except (UnknownLocaleError, ValueError):
        return Locale.parse("en")


def symbol_for(currency, cc):
    try:
        s = get_currency_symbol(currency, likely_locale(cc))
    except Exception:
        s = currency
    s = s.replace("\u200f", "").replace("\u200e", "").replace("\uffe5", "\u00a5").strip()
    if not s or any(ch not in SAFE_SYMBOL for ch in s):
        return currency
    # A bare "$" or "kr" means the local currency to people in that country, which is who sees it.
    return s


def main(countries_path, gdp_path, fx_path):
    _, cs = json.load(open(countries_path))
    real = {c["id"]: c for c in cs if c["region"]["value"].strip() != "Aggregates"}
    meta, rows = json.load(open(gdp_path))
    gdp = {}
    for r in rows:
        c = real.get(r["countryiso3code"])
        if c and r["value"] is not None:
            gdp[c["iso2Code"]] = r["value"]
    us = gdp["US"]
    fx = json.load(open(fx_path))
    rates = fx["rates"]

    codes = sorted({c.alpha_2 for c in pycountry.countries} | {"XK"})
    today = datetime.date.today()
    entries, currencies, notes = [], {}, []
    for cc in codes:
        if cc in BASE_PRICE:
            value = us
        elif cc in gdp:
            value = gdp[cc]
        elif cc in INHERIT:
            value = gdp[INHERIT[cc]]
        elif cc in ESTIMATES:
            value = ESTIMATES[cc]
        else:
            raise SystemExit(f"No income data for {cc}")
        m = max(FLOOR, min(1.0, (value / us) ** ELASTICITY))

        currency = CURRENCY_OVERRIDES.get(cc)
        if currency is None:
            found = get_territory_currencies(cc, today, tender=True) if cc != "XK" else ["EUR"]
            currency = found[0] if found else "USD"
        if currency not in rates:
            notes.append(f"{cc}: no rate for {currency}, priced in USD")
            currency = "USD"
        if currency not in currencies:
            digits = get_currency_precision(currency)
            style = "r" if currency in ROUND_STYLE or digits == 0 else "w" if currency in WHOLE_STYLE else "n"
            currencies[currency] = (rates[currency], digits, symbol_for(currency, cc), style)
        entries.append(f"{cc};{m:.2f};{currency}")

    # The symbol is the one used where the currency is at home (the euro's in Germany, the dollar's in the US).
    homes = {"EUR": "DE", "USD": "US", "XAF": "CM", "XOF": "SN", "XCD": "AG", "XPF": "PF", "AUD": "AU", "NZD": "NZ",
             "GBP": "GB", "CHF": "CH", "DKK": "DK", "NOK": "NO", "INR": "IN", "ZAR": "ZA", "ANG": "CW", "ILS": "IL",
             "JOD": "JO", "MAD": "MA"}
    for cur, cc in homes.items():
        if cur in currencies:
            rate, digits, _, style = currencies[cur]
            currencies[cur] = (rate, digits, symbol_for(cur, cc), style)

    def lines(items, per_line):
        out = []
        for i in range(0, len(items), per_line):
            out.append('"' + "|".join(items[i:i + per_line]) + '|"')
        return "\n            + ".join(out)

    cur_items = [f"{code};{rate:.6g};{digits};{sym};{style}" for code, (rate, digits, sym, style) in sorted(currencies.items())]
    updated = fx.get("time_last_update_utc", "")
    src = f'''// <auto-generated>
// Generated by Tools/build_price_table.py - do not edit by hand, re-run the script instead.
// Income: World Bank WDI, GDP per capita (current US$), NY.GDP.PCAP.CD, updated {meta.get("lastupdated")} (CC BY 4.0), with
// estimates for the territories it doesn't list. multiplier = clamp((GDP per capita / US)^{ELASTICITY}, {FLOOR}, 1.0).
// Currencies: ISO 3166-1 (pycountry) and CLDR (babel). Exchange rates: ExchangeRate-API (open.er-api.com), {updated}.
// </auto-generated>
namespace Casino.Core.Economy
{{
    internal static class RegionalPriceTable
    {{
        public const string DataSource = "World Bank GDP per capita (updated {meta.get("lastupdated")}); exchange rates of {updated[:16]}";

        // ISO 3166-1 alpha-2 ; price multiplier relative to the base (USD) price ; ISO 4217 currency
        public const string Countries =
            {lines(entries, 9)};

        // ISO 4217 ; units per US dollar ; decimals ; symbol ; price style (n: cents under 100, else ending in 9;
        // w: whole units ending in 9; r: round numbers)
        public const string Currencies =
            {lines(cur_items, 6)};
    }}
}}
'''
    open(OUT, "w", encoding="utf-8").write(src)
    for n in notes:
        print("note:", n)
    print(f"{len(entries)} countries, {len(currencies)} currencies -> {OUT}")


if __name__ == "__main__":
    main(*sys.argv[1:4])
