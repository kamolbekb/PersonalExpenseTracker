import {
	createBrowserRouter,
	RouterProvider,
	NavLink,
	Outlet,
} from "react-router-dom";
import AddExpense from "./screens/AddExpense";
import Expenses from "./screens/Expenses";
import Categories from "./screens/Categories";
import Budgets from "./screens/Budgets";
import Reports from "./screens/Reports";
import Settings from "./screens/Settings";

function Layout() {
	return (
		<div className="app">
			<main>
				<Outlet />
			</main>
			<nav className="tabbar">
				<NavLink to="/">Add</NavLink>
				<NavLink to="/expenses">List</NavLink>
				<NavLink to="/categories">🏷️</NavLink>
				<NavLink to="/reports">Reports</NavLink>
				<NavLink to="/budgets">Budgets</NavLink>
				<NavLink to="/settings">⚙</NavLink>
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
		],
	},
]);

export const AppRouter = () => <RouterProvider router={router} />;
