# Masterclass Quiz

A live quiz app for teaching a group: in a Masterclass, a Host presents each Workshop of a Course, then the group answers a quiz on it and each person learns which Lessons to revisit.

## Course content

**Course**:
A body of teaching material made of ordered Workshops (e.g. "Testing Fundamentals").
_Avoid_: Training, Masterclass (a Masterclass is one group being taught a Course)

**Workshop**:
One part of a Course, taught in a single talk and followed by a Session (e.g. "Unit tests", "Test doubles").
_Avoid_: Section, Chapter, Module, Lesson (a Lesson is one piece inside a Workshop)

**Lesson**:
A single piece of material inside a Workshop, the smallest thing a Participant can be pointed back to (e.g. "Writing your first test"). An exercise and its solution together are one Lesson.
_Avoid_: Page, Article, Topic

**Question**:
A prompt with answer options, exactly one of them correct, that tests one Lesson. A Workshop's Questions are all the Questions of its Lessons.
_Avoid_: Item, Task

**Difficulty**:
How hard a Question is: Easy, Medium or Hard. Harder Questions give more time to answer and are worth more points.
_Avoid_: Level, Weight

## Live quiz

**Masterclass**:
One group being taught a Course in a single meeting, ending when the Host closes it; it holds that group's Sessions and results (e.g. "Dev team 2026"). The same Course can be taught in many Masterclasses.
_Avoid_: Course, Cohort, Class, Group

**Session**:
The one live quiz for a Workshop within a Masterclass, run by a Host after presenting it, using a random selection of that Workshop's Questions in the Difficulty mix the Host sets for it.
_Avoid_: Game, Round, Quiz

**Answer**:
A Participant's response to one Question in a Session; its outcome is Correct, Incorrect or Timed out (no answer before time ran out).
_Avoid_: Submission, Response, Not answered, Skipped

**Recap**:
A Participant's personal summary of a Masterclass, by Workshop: the Questions they got Incorrect or Timed out, each pointing to the Lesson to revisit, or Not attended for Workshops whose Session they missed.
_Avoid_: Report (ambiguous with the Host's results), Feedback, Summary

**Not attended**:
A Participant missing a Workshop's whole Session, e.g. by joining the Masterclass later; their Recap points them to the Workshop's Lessons instead.
_Avoid_: Absent, Skipped

**Perfect score**:
A Participant answering every Question in a Session Correct; shown as a badge in their Recap.
_Avoid_: Flawless, Clean sheet

## People

**Host**:
The person who presents a Workshop and runs its Session.
_Avoid_: Admin, Presenter, Teacher

**Participant**:
A person in a Masterclass answering Questions in its Sessions, known only by an assigned Nickname that stays the same across the whole Masterclass.
_Avoid_: Player, User, Student, Attendee

**Join code**:
The short code Participants enter to join a Masterclass.
_Avoid_: PIN, Room code, Session code

**Nickname**:
The name the app assigns to a Participant on joining; Participants do not choose it.
_Avoid_: Username, Display name
