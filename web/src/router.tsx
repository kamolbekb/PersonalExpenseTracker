import {
	createBrowserRouter,
	RouterProvider,
	NavLink,
	Outlet,
	useLocation,
	useNavigate,
} from "react-router-dom";
import AddExpense from "./screens/AddExpense";
import Expenses from "./screens/Expenses";
import Categories from "./screens/Categories";
import Budgets from "./screens/Budgets";
import Reports from "./screens/Reports";
import Settings from "./screens/Settings";
import Rates from "./screens/Rates";
import {
	IconAdd,
	IconList,
	IconReports,
	IconBudgets,
	IconRates,
	IconSettings,
} from "./components/icons";

const TABS = [
	{ to: "/", label: "Add", Icon: IconAdd, end: true },
	{ to: "/expenses", label: "Expenses", Icon: IconList },
	{ to: "/reports", label: "Reports", Icon: IconReports },
	{ to: "/budgets", label: "Budgets", Icon: IconBudgets },
	{ to: "/rates", label: "Rates", Icon: IconRates },
];

const TITLES: Record<string, string> = {
	"/": "Add expense",
	"/expenses": "Expenses",
	"/reports": "Reports",
	"/budgets": "Budgets",
	"/rates": "Rates & gold",
	"/settings": "Settings",
	"/categories": "Categories",
};

function Layout() {
	const { pathname } = useLocation();
	const navigate = useNavigate();
	const title = TITLES[pathname] ?? "Expenses";

	return (
		<div className="app">
			<header className="app__header">
				<h1 className="app__title">{title}</h1>
				<button
					className="icon-btn"
					aria-label="Settings"
					onClick={() => navigate("/settings")}
				>
					<IconSettings />
				</button>
			</header>

			<main className="app__main">
				<Outlet />
			</main>

			<nav className="tabbar">
				<div className="tabbar__inner">
					{TABS.map(({ to, label, Icon, end }) => (
						<NavLink key={to} to={to} end={end}>
							<Icon />
							{label}
						</NavLink>
					))}
				</div>
			</nav>
		</div>
	);
}

const router = createBrowserRouter([
	{
		path: "/",
		element: <Layout />,
		children: [
			{ index: true, element: <AddExpense /> },
			{ path: "expenses", element: <Expenses /> },
			{ path: "categories", element: <Categories /> },
			{ path: "budgets", element: <Budgets /> },
			{ path: "reports", element: <Reports /> },
			{ path: "settings", element: <Settings /> },
			{ path: "rates", element: <Rates /> },
		],
	},
]);

export const AppRouter = () => <RouterProvider router={router} />;
