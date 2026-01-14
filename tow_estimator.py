"""Tow estimator desktop app.

Portable Windows-friendly UI that stores logs next to the executable.
"""

from __future__ import annotations

import json
import sys
import threading
from dataclasses import dataclass, asdict
from datetime import datetime
from pathlib import Path
from tkinter import Listbox, StringVar, Tk, messagebox
from tkinter import ttk
from urllib.parse import urlencode
from urllib.request import urlopen


GEOAPIFY_KEY = "7305e94ac22249c1b3224802b9c1409d"
COUNTRY_FILTER = "us"
APP_TITLE = "Southern Pride Tow Estimator"


def app_directory() -> Path:
    if getattr(sys, "frozen", False):
        return Path(sys.executable).resolve().parent
    return Path(__file__).resolve().parent


LOG_PATH = app_directory() / "tow_estimator_logs.jsonl"
SETTINGS_PATH = app_directory() / "tow_estimator_settings.json"


@dataclass
class AppSettings:
    theme: str = "light"
    deadhead_address: str = "184 Nicholson Rd, Lincolnton, NC 28092"
    base_fee: str = "30"
    rate_per_mile: str = "3.50"

    @classmethod
    def load(cls) -> "AppSettings":
        if not SETTINGS_PATH.exists():
            return cls()
        try:
            data = json.loads(SETTINGS_PATH.read_text(encoding="utf-8"))
        except (OSError, json.JSONDecodeError):
            return cls()
        return cls(
            theme=data.get("theme", "light"),
            deadhead_address=data.get("deadhead_address", cls.deadhead_address),
            base_fee=data.get("base_fee", cls.base_fee),
            rate_per_mile=data.get("rate_per_mile", cls.rate_per_mile),
        )

    def save(self) -> None:
        SETTINGS_PATH.write_text(json.dumps(asdict(self)), encoding="utf-8")


@dataclass
class Quote:
    created_at: str
    customer_name: str
    customer_phone: str
    pickup_address: str
    dropoff_address: str
    deadhead_address: str
    base_fee: float
    rate_per_mile: float
    deadhead_miles: float
    tow_miles: float
    total_miles: float
    estimated_total: float
    notes: str


def format_currency(amount: float) -> str:
    return f"${amount:,.2f}"


def fetch_json(url: str) -> dict:
    with urlopen(url, timeout=12) as response:
        return json.loads(response.read().decode("utf-8"))


def geocode(address: str) -> dict | None:
    if not address.strip():
        return None
    params = {
        "text": address,
        "apiKey": GEOAPIFY_KEY,
        "limit": 1,
    }
    if COUNTRY_FILTER:
        params["filter"] = f"countrycode:{COUNTRY_FILTER}"
    url = "https://api.geoapify.com/v1/geocode/search?" + urlencode(params)
    data = fetch_json(url)
    features = data.get("features", [])
    if not features:
        return None
    props = features[0].get("properties", {})
    return {
        "formatted": props.get("formatted", address),
        "lat": props.get("lat"),
        "lon": props.get("lon"),
    }


def fetch_autocomplete(text: str) -> list[str]:
    if not text.strip():
        return []
    params = {
        "text": text,
        "apiKey": GEOAPIFY_KEY,
        "limit": 8,
    }
    if COUNTRY_FILTER:
        params["filter"] = f"countrycode:{COUNTRY_FILTER}"
    url = "https://api.geoapify.com/v1/geocode/autocomplete?" + urlencode(params)
    data = fetch_json(url)
    suggestions = []
    for feature in data.get("features", []):
        formatted = feature.get("properties", {}).get("formatted")
        if formatted:
            suggestions.append(formatted)
    return suggestions


def route_miles(start: dict, end: dict) -> float:
    params = {
        "waypoints": f"{start['lat']},{start['lon']}|{end['lat']},{end['lon']}",
        "mode": "drive",
        "apiKey": GEOAPIFY_KEY,
    }
    url = "https://api.geoapify.com/v1/routing?" + urlencode(params)
    data = fetch_json(url)
    meters = (
        data.get("features", [{}])[0]
        .get("properties", {})
        .get("distance")
    )
    if not isinstance(meters, (float, int)):
        raise ValueError("Routing distance unavailable.")
    return meters / 1609.34


class TowEstimatorApp:
    def __init__(self, root: Tk) -> None:
        self.root = root
        self.root.title(APP_TITLE)
        self.root.geometry("980x640")
        self.root.minsize(920, 600)

        self.settings = AppSettings.load()
        self.theme = StringVar(value=self.settings.theme)
        self.status_text = StringVar(value="Ready.")

        self.customer_name = StringVar()
        self.customer_phone = StringVar()
        self.deadhead_address = StringVar(value=self.settings.deadhead_address)
        self.pickup_address = StringVar()
        self.dropoff_address = StringVar()
        self.base_fee = StringVar(value=self.settings.base_fee)
        self.rate_per_mile = StringVar(value=self.settings.rate_per_mile)
        self.notes = StringVar()

        self.result_summary = StringVar(value="Enter addresses and click Estimate.")
        self.latest_quote: Quote | None = None

        self.search_query = StringVar()
        self._autocomplete_jobs: dict[ttk.Entry, str] = {}

        self._setup_style()
        self._build_layout()
        self._bind_setting_traces()
        self._load_quotes()

    def _setup_style(self) -> None:
        style = ttk.Style()
        style.theme_use("clam")
        self._apply_theme(style)

    def _apply_theme(self, style: ttk.Style) -> None:
        theme = self.theme.get()
        theme_map = {
            "light": {
                "bg": "#f8fafc",
                "card": "#ffffff",
                "text": "#0f172a",
                "muted": "#64748b",
                "accent": "#2563eb",
                "button": "#1d4ed8",
                "button_text": "#ffffff",
            },
            "dark": {
                "bg": "#0f172a",
                "card": "#111827",
                "text": "#f8fafc",
                "muted": "#94a3b8",
                "accent": "#38bdf8",
                "button": "#0ea5e9",
                "button_text": "#0f172a",
            },
            "sunset": {
                "bg": "#fff7ed",
                "card": "#ffedd5",
                "text": "#431407",
                "muted": "#9a3412",
                "accent": "#fb923c",
                "button": "#ea580c",
                "button_text": "#fff7ed",
            },
            "neon": {
                "bg": "#0f172a",
                "card": "#1f2937",
                "text": "#e0f2fe",
                "muted": "#94a3b8",
                "accent": "#a78bfa",
                "button": "#22d3ee",
                "button_text": "#0f172a",
            },
        }
        colors = theme_map.get(theme, theme_map["light"])

        self.root.configure(background=colors["bg"])
        style.configure("TFrame", background=colors["bg"])
        style.configure("Card.TFrame", background=colors["card"], relief="flat")
        style.configure("Header.TFrame", background=colors["accent"])
        style.configure("TLabel", background=colors["bg"], foreground=colors["text"])
        style.configure(
            "Header.TLabel",
            background=colors["accent"],
            foreground=colors["button_text"],
            font=("Segoe UI", 18, "bold"),
        )
        style.configure(
            "Header.Subtitle.TLabel",
            background=colors["accent"],
            foreground=colors["button_text"],
            font=("Segoe UI", 11),
        )
        style.configure("Muted.TLabel", foreground=colors["muted"])
        style.configure(
            "Heading.TLabel",
            font=("Segoe UI", 18, "bold"),
            foreground=colors["text"],
        )
        style.configure(
            "Accent.TLabel",
            font=("Segoe UI", 12, "bold"),
            foreground=colors["accent"],
        )
        style.configure(
            "TButton",
            background=colors["button"],
            foreground=colors["button_text"],
            padding=8,
        )
        style.map(
            "TButton",
            background=[("active", colors["accent"]), ("disabled", "#94a3b8")],
        )
        style.configure(
            "TEntry",
            fieldbackground=colors["card"],
            foreground=colors["text"],
        )
        style.configure(
            "Treeview",
            background=colors["card"],
            fieldbackground=colors["card"],
            foreground=colors["text"],
            borderwidth=0,
            rowheight=28,
            font=("Segoe UI", 10),
        )
        style.configure(
            "Treeview.Heading",
            font=("Segoe UI", 10, "bold"),
            background=colors["bg"],
            foreground=colors["text"],
        )
        self._update_tree_tags(colors)

    def _build_layout(self) -> None:
        header = ttk.Frame(self.root, padding=(24, 18), style="Header.TFrame")
        header.pack(fill="x")
        ttk.Label(header, text="Southern Pride Towing", style="Header.TLabel").pack(
            anchor="w"
        )
        ttk.Label(
            header,
            text="Flashy estimator + portable quote logs",
            style="Header.Subtitle.TLabel",
        ).pack(anchor="w")

        notebook = ttk.Notebook(self.root)
        notebook.pack(fill="both", expand=True, padx=18, pady=(0, 18))

        self.estimate_tab = ttk.Frame(notebook, padding=18)
        self.quotes_tab = ttk.Frame(notebook, padding=18)
        self.settings_tab = ttk.Frame(notebook, padding=18)
        notebook.add(self.estimate_tab, text="Estimate")
        notebook.add(self.quotes_tab, text="Quotes")
        notebook.add(self.settings_tab, text="Settings")

        self._build_estimate_tab()
        self._build_quotes_tab()
        self._build_settings_tab()

        status_bar = ttk.Frame(self.root, padding=(18, 10))
        status_bar.pack(fill="x")
        ttk.Label(status_bar, textvariable=self.status_text, style="Muted.TLabel").pack(
            anchor="w"
        )

    def _build_estimate_tab(self) -> None:
        layout = ttk.Frame(self.estimate_tab)
        layout.pack(fill="both", expand=True)

        left = ttk.Frame(layout)
        right = ttk.Frame(layout)
        left.pack(side="left", fill="both", expand=True, padx=(0, 16))
        right.pack(side="right", fill="both", expand=True)

        form = ttk.Frame(left, style="Card.TFrame", padding=18)
        form.pack(fill="both", expand=True)

        ttk.Label(form, text="Customer", style="Accent.TLabel").pack(anchor="w")
        self._entry(form, "Customer name", self.customer_name)
        self._entry(form, "Customer phone", self.customer_phone)
        ttk.Label(form, text="Job", style="Accent.TLabel").pack(anchor="w", pady=(10, 0))
        self._entry(form, "Deadhead start (yard)", self.deadhead_address, autocomplete=True)
        self._entry(form, "Pickup address", self.pickup_address, autocomplete=True)
        self._entry(form, "Drop-off address", self.dropoff_address, autocomplete=True)
        ttk.Label(form, text="Rates", style="Accent.TLabel").pack(anchor="w", pady=(10, 0))
        self._entry(form, "Base fee", self.base_fee)
        self._entry(form, "Rate per mile", self.rate_per_mile)
        self._entry(form, "Notes", self.notes)

        actions = ttk.Frame(form)
        actions.pack(fill="x", pady=(14, 0))
        ttk.Button(actions, text="Estimate", command=self.run_estimate).pack(
            side="left"
        )
        ttk.Button(actions, text="Save Quote", command=self.save_quote).pack(
            side="left", padx=10
        )
        ttk.Button(actions, text="Clear", command=self.clear_form).pack(side="left")

        results = ttk.Frame(right, style="Card.TFrame", padding=18)
        results.pack(fill="both", expand=True)
        ttk.Label(results, text="Estimate Preview", style="Accent.TLabel").pack(
            anchor="w"
        )
        ttk.Label(
            results, textvariable=self.result_summary, wraplength=360, justify="left"
        ).pack(anchor="w", pady=(12, 0))
        ttk.Label(
            results,
            text="Powered by Geoapify routing",
            style="Muted.TLabel",
        ).pack(anchor="w", pady=(18, 0))

    def _build_quotes_tab(self) -> None:
        top = ttk.Frame(self.quotes_tab)
        top.pack(fill="x")
        ttk.Label(top, text="Search logs", style="Accent.TLabel").pack(anchor="w")
        search_row = ttk.Frame(top)
        search_row.pack(fill="x", pady=(6, 12))
        ttk.Entry(search_row, textvariable=self.search_query).pack(
            side="left", fill="x", expand=True
        )
        ttk.Button(search_row, text="Search", command=self.filter_quotes).pack(
            side="left", padx=8
        )
        ttk.Button(search_row, text="Reset", command=self.reset_search).pack(
            side="left"
        )

        self.tree = ttk.Treeview(
            self.quotes_tab,
            columns=(
                "created",
                "name",
                "phone",
                "pickup",
                "dropoff",
                "total",
            ),
            show="headings",
            height=14,
        )
        for key, label, width in [
            ("created", "Created", 140),
            ("name", "Customer", 140),
            ("phone", "Phone", 120),
            ("pickup", "Pickup", 200),
            ("dropoff", "Drop-off", 200),
            ("total", "Total", 90),
        ]:
            self.tree.heading(key, text=label)
            self.tree.column(key, width=width, anchor="w")
        self.tree.pack(fill="both", expand=True)
        self.tree.bind("<<TreeviewSelect>>", self._show_selected_quote)
        self._update_tree_tags(None)

        self.details = ttk.Label(
            self.quotes_tab, text="Select a quote to see details.", wraplength=900
        )
        self.details.pack(fill="x", pady=(10, 0))

    def _build_settings_tab(self) -> None:
        card = ttk.Frame(self.settings_tab, style="Card.TFrame", padding=18)
        card.pack(fill="both", expand=False, anchor="nw")
        ttk.Label(card, text="Theme", style="Accent.TLabel").pack(anchor="w")
        theme_row = ttk.Frame(card)
        theme_row.pack(anchor="w", pady=(8, 12))
        for label, value in [
            ("Light", "light"),
            ("Dark", "dark"),
            ("Sunset", "sunset"),
            ("Neon", "neon"),
        ]:
            ttk.Radiobutton(
                theme_row,
                text=label,
                value=value,
                variable=self.theme,
                command=self.apply_theme,
            ).pack(side="left", padx=6)

        ttk.Label(
            card,
            text=f"Logs stored at: {LOG_PATH}",
            style="Muted.TLabel",
            wraplength=600,
        ).pack(anchor="w")

    def _entry(
        self,
        parent: ttk.Frame,
        label: str,
        variable: StringVar,
        *,
        autocomplete: bool = False,
    ) -> None:
        ttk.Label(parent, text=label).pack(anchor="w", pady=(6, 0))
        field_frame = ttk.Frame(parent)
        field_frame.pack(fill="x")
        entry = ttk.Entry(field_frame, textvariable=variable)
        entry.pack(fill="x")
        if autocomplete:
            listbox = Listbox(
                field_frame,
                height=5,
                relief="flat",
                highlightthickness=1,
                activestyle="none",
            )
            listbox.pack(fill="x", pady=(2, 0))
            listbox.pack_forget()
            self._attach_autocomplete(entry, listbox, variable)

    def apply_theme(self) -> None:
        self._apply_theme(ttk.Style())
        self._save_settings()

    def _update_tree_tags(self, colors: dict | None) -> None:
        if not getattr(self, "tree", None):
            return
        if colors and colors["bg"] == "#0f172a":
            odd = "#1e293b"
            even = "#0f172a"
        elif colors and colors["bg"] == "#fff7ed":
            odd = "#fed7aa"
            even = "#ffedd5"
        else:
            odd = "#e2e8f0"
            even = "#f8fafc"
        self.tree.tag_configure("odd", background=odd)
        self.tree.tag_configure("even", background=even)

    def _bind_setting_traces(self) -> None:
        for variable in [self.deadhead_address, self.base_fee, self.rate_per_mile]:
            variable.trace_add("write", lambda *_: self._save_settings())

    def _save_settings(self) -> None:
        self.settings = AppSettings(
            theme=self.theme.get(),
            deadhead_address=self.deadhead_address.get(),
            base_fee=self.base_fee.get(),
            rate_per_mile=self.rate_per_mile.get(),
        )
        try:
            self.settings.save()
        except OSError:
            self.status_text.set("Could not save settings.")

    def _attach_autocomplete(
        self, entry: ttk.Entry, listbox: Listbox, variable: StringVar
    ) -> None:
        def on_key_release(_event: object) -> None:
            job = self._autocomplete_jobs.get(entry)
            if job:
                self.root.after_cancel(job)
            self._autocomplete_jobs[entry] = self.root.after(
                250, lambda: self._run_autocomplete(entry, listbox, variable)
            )

        def on_select(_event: object) -> None:
            selection = listbox.curselection()
            if not selection:
                return
            value = listbox.get(selection[0])
            variable.set(value)
            listbox.pack_forget()

        def hide_list(_event: object) -> None:
            self.root.after(150, listbox.pack_forget)

        entry.bind("<KeyRelease>", on_key_release)
        entry.bind("<FocusIn>", on_key_release)
        entry.bind("<FocusOut>", hide_list)
        listbox.bind("<<ListboxSelect>>", on_select)

    def _run_autocomplete(
        self, entry: ttk.Entry, listbox: Listbox, variable: StringVar
    ) -> None:
        query = variable.get().strip()
        if not query:
            listbox.pack_forget()
            return

        def worker() -> None:
            try:
                suggestions = fetch_autocomplete(query)
            except Exception:
                suggestions = []
            self.root.after(
                0, lambda: self._show_suggestions(listbox, suggestions)
            )

        threading.Thread(target=worker, daemon=True).start()

    def _show_suggestions(self, listbox: Listbox, suggestions: list[str]) -> None:
        listbox.delete(0, "end")
        if not suggestions:
            listbox.pack_forget()
            return
        for item in suggestions:
            listbox.insert("end", item)
        listbox.pack(fill="x", pady=(2, 0))

    def clear_form(self) -> None:
        self.customer_name.set("")
        self.customer_phone.set("")
        self.pickup_address.set("")
        self.dropoff_address.set("")
        self.notes.set("")
        self.result_summary.set("Enter addresses and click Estimate.")
        self.latest_quote = None
        self.status_text.set("Cleared form.")

    def run_estimate(self) -> None:
        if not self.pickup_address.get().strip() or not self.dropoff_address.get().strip():
            messagebox.showwarning("Missing info", "Pickup and drop-off are required.")
            return
        self.status_text.set("Calculating estimate...")
        threading.Thread(target=self._estimate_worker, daemon=True).start()

    def _estimate_worker(self) -> None:
        try:
            deadhead = geocode(self.deadhead_address.get())
            pickup = geocode(self.pickup_address.get())
            dropoff = geocode(self.dropoff_address.get())
            if not all([deadhead, pickup, dropoff]):
                raise ValueError("Could not geocode one of the addresses.")

            dead_miles = route_miles(deadhead, pickup)
            tow_miles = route_miles(pickup, dropoff)
            total_miles = dead_miles + tow_miles

            base_fee = float(self.base_fee.get() or "0")
            rate = float(self.rate_per_mile.get() or "0")
            estimate = base_fee + (total_miles * rate)

            quote = Quote(
                created_at=datetime.now().strftime("%Y-%m-%d %H:%M"),
                customer_name=self.customer_name.get().strip(),
                customer_phone=self.customer_phone.get().strip(),
                pickup_address=pickup["formatted"],
                dropoff_address=dropoff["formatted"],
                deadhead_address=deadhead["formatted"],
                base_fee=base_fee,
                rate_per_mile=rate,
                deadhead_miles=dead_miles,
                tow_miles=tow_miles,
                total_miles=total_miles,
                estimated_total=estimate,
                notes=self.notes.get().strip(),
            )

            self.latest_quote = quote
            summary = (
                f"Deadhead: {dead_miles:.1f} mi\n"
                f"Tow: {tow_miles:.1f} mi\n"
                f"Total miles: {total_miles:.1f} mi\n\n"
                f"Estimated total: {format_currency(estimate)}\n\n"
                f"Pickup: {quote.pickup_address}\n"
                f"Drop-off: {quote.dropoff_address}"
            )
            self.result_summary.set(summary)
            self.status_text.set("Estimate ready.")
        except Exception as exc:  # noqa: BLE001 - surfaced to user
            self.status_text.set("Estimate failed.")
            messagebox.showerror("Estimate error", str(exc))

    def save_quote(self) -> None:
        if not self.latest_quote:
            messagebox.showwarning("No estimate", "Run an estimate before saving.")
            return
        try:
            LOG_PATH.parent.mkdir(parents=True, exist_ok=True)
            with LOG_PATH.open("a", encoding="utf-8") as handle:
                handle.write(json.dumps(asdict(self.latest_quote)) + "\n")
            self.status_text.set("Quote saved to portable log.")
            self._load_quotes()
        except OSError as exc:
            messagebox.showerror("Save error", str(exc))

    def _load_quotes(self) -> None:
        self.tree.delete(*self.tree.get_children())
        self._quotes = []
        if not LOG_PATH.exists():
            return
        with LOG_PATH.open("r", encoding="utf-8") as handle:
            for line in handle:
                if not line.strip():
                    continue
                try:
                    payload = json.loads(line)
                    self._quotes.append(payload)
                except json.JSONDecodeError:
                    continue
        for index, quote in enumerate(self._quotes):
            tag = "even" if index % 2 == 0 else "odd"
            self.tree.insert(
                "",
                "end",
                values=(
                    quote.get("created_at"),
                    quote.get("customer_name"),
                    quote.get("customer_phone"),
                    quote.get("pickup_address"),
                    quote.get("dropoff_address"),
                    format_currency(quote.get("estimated_total", 0)),
                ),
                tags=(tag,),
            )

    def filter_quotes(self) -> None:
        query = self.search_query.get().strip().lower()
        if not query:
            self._load_quotes()
            return
        filtered = []
        for quote in self._quotes:
            haystack = " ".join(
                str(quote.get(field, ""))
                for field in [
                    "customer_name",
                    "customer_phone",
                    "pickup_address",
                    "dropoff_address",
                ]
            ).lower()
            if query in haystack:
                filtered.append(quote)
        self.tree.delete(*self.tree.get_children())
        for index, quote in enumerate(filtered):
            tag = "even" if index % 2 == 0 else "odd"
            self.tree.insert(
                "",
                "end",
                values=(
                    quote.get("created_at"),
                    quote.get("customer_name"),
                    quote.get("customer_phone"),
                    quote.get("pickup_address"),
                    quote.get("dropoff_address"),
                    format_currency(quote.get("estimated_total", 0)),
                ),
                tags=(tag,),
            )
        self.status_text.set(f"Filtered to {len(filtered)} quotes.")

    def reset_search(self) -> None:
        self.search_query.set("")
        self._load_quotes()
        self.status_text.set("Search reset.")

    def _show_selected_quote(self, _event: object) -> None:
        selection = self.tree.selection()
        if not selection:
            return
        index = self.tree.index(selection[0])
        if index >= len(self.tree.get_children()):
            return
        item = self.tree.item(selection[0])
        created, name, phone, pickup, dropoff, total = item.get("values", [""] * 6)
        self.details.configure(
            text=(
                f"{created} | {name} | {phone}\n"
                f"Pickup: {pickup}\nDrop-off: {dropoff}\nTotal: {total}"
            )
        )


def main() -> None:
    root = Tk()
    app = TowEstimatorApp(root)
    root.after(100, lambda: app.status_text.set("Ready to estimate."))
    root.mainloop()


if __name__ == "__main__":
    main()
