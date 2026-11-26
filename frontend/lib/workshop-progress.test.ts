import { describe, expect, it } from "vitest";
import type { WorkshopOverview } from "./courses";
import { workshopProgress } from "./workshop-progress";

const questions = { easy: 2, medium: 1, hard: 1 };
const unitTests: WorkshopOverview = { id: 11, slug: "unit-tests", title: "Unit tests", number: 1, questions };
const testDoubles: WorkshopOverview = { id: 12, slug: "test-doubles", title: "Test doubles", number: 2, questions };
const integration: WorkshopOverview = { id: 13, slug: "integration-tests", title: "Integration tests", number: 3, questions };

const steps = (progress: ReturnType<typeof workshopProgress>) => progress.map((p) => [p.workshop.title, p.step]);

describe("where a Masterclass has got to in its Course", () => {
  it("shows Workshops that had their Session as done, the Next Workshop as next and the rest as locked", () => {
    expect(steps(workshopProgress([unitTests, testDoubles, integration], testDoubles.id, [unitTests.id]))).toEqual([
      ["Unit tests", "done"],
      ["Test doubles", "next"],
      ["Integration tests", "locked"],
    ]);
  });

  it("goes in Course order whatever order the Workshops come in", () => {
    expect(steps(workshopProgress([integration, unitTests, testDoubles], unitTests.id, []))).toEqual([
      ["Unit tests", "next"],
      ["Test doubles", "locked"],
      ["Integration tests", "locked"],
    ]);
  });

  it("orders Workshops with the same number by id, as the backend does", () => {
    const sameNumber = { ...integration, number: 2 };
    expect(steps(workshopProgress([sameNumber, testDoubles, unitTests], unitTests.id, []))).toEqual([
      ["Unit tests", "next"],
      ["Test doubles", "locked"],
      ["Integration tests", "locked"],
    ]);
  });

  it("keeps a Workshop done after a re-import moves it after the Next Workshop", () => {
    const movedLast = { ...unitTests, number: 4 };
    expect(steps(workshopProgress([movedLast, testDoubles, integration], testDoubles.id, [unitTests.id]))).toEqual([
      ["Test doubles", "next"],
      ["Integration tests", "locked"],
      ["Unit tests", "done"],
    ]);
  });

  it("shows every Workshop as done once there is no Next Workshop", () => {
    expect(steps(workshopProgress([unitTests, testDoubles], null, [testDoubles.id, unitTests.id]))).toEqual([
      ["Unit tests", "done"],
      ["Test doubles", "done"],
    ]);
  });
});
