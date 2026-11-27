import { Link } from "react-router";
import { useTitle } from "../ui";

export function NotFound() {
  useTitle("Siden findes ikke");
  return (
    <div className="narrow">
      <h1>Siden findes ikke</h1>
      <p><Link to="/">Til opgaverne</Link></p>
    </div>
  );
}
